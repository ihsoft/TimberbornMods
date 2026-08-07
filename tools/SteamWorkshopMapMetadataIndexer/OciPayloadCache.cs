// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

#nullable enable
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed class OciPayloadCache : IDisposable {
  const ulong ShardCount = 100;
  const string CatalogTag = "catalog-v1";
  const string ArtifactType = "application/vnd.timberborn.workshop-payload-cache.v1";
  const string PayloadMediaType = "application/vnd.timberborn.map-payload.v1+zip";
  const string CatalogMediaType = "application/vnd.timberborn.workshop-payload-catalog.v1+json";
  const string ManifestMediaType = "application/vnd.oci.image.manifest.v1+json";
  const string ConfigMediaType = "application/vnd.oci.empty.v1+json";
  static readonly TimeSpan[] RegistryWriteRetryDelays = [TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40)];

  sealed class PayloadCacheException(string message, Exception? innerException = null)
      : Exception(message, innerException);

  sealed record CatalogEntry(string Shard, string Digest, long Size, string Sha256);
  sealed record OrasCommandResult(bool Success, string Output);
  sealed record OciManifest(
      [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
      [property: JsonPropertyName("mediaType")] string MediaType,
      [property: JsonPropertyName("artifactType")] string ArtifactType,
      [property: JsonPropertyName("config")] OciDescriptor Config,
      [property: JsonPropertyName("layers")] IReadOnlyList<OciDescriptor> Layers);
  sealed record OciDescriptor(
      [property: JsonPropertyName("mediaType")] string MediaType,
      [property: JsonPropertyName("digest")] string Digest,
      [property: JsonPropertyName("size")] long Size,
      [property: JsonPropertyName("annotations"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
      IReadOnlyDictionary<string, string>? Annotations = null);

  readonly string _repository;
  readonly string _workDirectory;
  readonly Dictionary<string, CatalogEntry> _catalog;
  readonly HashSet<string> _dirtyShards = [];
  bool _catalogDirty;
  int _flushFailures;
  int _writeFailures;

  OciPayloadCache(string repository, string workDirectory, Dictionary<string, CatalogEntry> catalog) {
    _repository = repository;
    _workDirectory = workDirectory;
    _catalog = catalog;
  }

  public int Count => _catalog.Count;

  /// <summary>Number of final cache publications that failed after all registry attempts.</summary>
  public int FlushFailures => _flushFailures;

  /// <summary>Number of downloaded payloads that could not be stored after all registry attempts.</summary>
  public int WriteFailures => _writeFailures;

  public static OciPayloadCache? CreateFromEnvironment() {
    var repository = Environment.GetEnvironmentVariable("MAP_PAYLOAD_CACHE_OCI_REPOSITORY");
    if (string.IsNullOrWhiteSpace(repository)) {
      Console.WriteLine("Payload OCI cache is not configured; continuing without it.");
      return null;
    }
    var workDirectory = Path.Combine(Path.GetTempPath(), $"timberborn-payload-cache-{Guid.NewGuid():N}");
    Directory.CreateDirectory(workDirectory);
    var catalogDirectory = Path.Combine(workDirectory, "catalog");
    Directory.CreateDirectory(catalogDirectory);
    var catalog = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
    var pull = RunOras(["pull", $"{repository}:{CatalogTag}", "--output", catalogDirectory], allowNotFound: true);
    if (pull.Success) {
      var path = Path.Combine(catalogDirectory, "catalog.json");
      if (!File.Exists(path)) {
        throw new InvalidDataException("OCI payload cache catalog artifact contains no catalog.json.");
      }
      catalog = JsonSerializer.Deserialize<Dictionary<string, CatalogEntry>>(File.ReadAllText(path))
          ?? throw new InvalidDataException("OCI payload cache catalog could not be parsed.");
    }
    Console.WriteLine($"Payload OCI cache connected; found {catalog.Count} cached map versions.");
    return new OciPayloadCache(repository, workDirectory, catalog);
  }

  public bool Contains(string publishedFileId, string? updatedAtUtc) {
    return _catalog.ContainsKey(CreateEntryName(publishedFileId, updatedAtUtc));
  }

  public byte[]? TryRead(string publishedFileId, string? updatedAtUtc, ulong maximumBytes) {
    var entryName = CreateEntryName(publishedFileId, updatedAtUtc);
    if (!_catalog.TryGetValue(entryName, out var entry)) {
      return null;
    }
    if (entry.Size < 0 || (ulong) entry.Size > maximumBytes) {
      throw new InvalidDataException($"Cached payload {entryName} has an unexpected size {entry.Size}.");
    }
    var path = Path.Combine(_workDirectory, $"read-{Guid.NewGuid():N}.timber");
    RunOras(["blob", "fetch", "--output", path, $"{_repository}@{entry.Digest}"]);
    var bytes = File.ReadAllBytes(path);
    var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    if (bytes.LongLength != entry.Size
        || !string.Equals(entry.Sha256, actualHash, StringComparison.OrdinalIgnoreCase)) {
      throw new InvalidDataException($"Cached payload {entryName} failed size or SHA-256 validation.");
    }
    return bytes;
  }

  /// <summary>
  /// Tries to store a downloaded payload without allowing a registry failure to interrupt map analysis.
  /// </summary>
  public bool TryWrite(string publishedFileId, string? updatedAtUtc, Stream payload) {
    var entryName = CreateEntryName(publishedFileId, updatedAtUtc);
    var path = Path.Combine(_workDirectory, $"write-{Guid.NewGuid():N}.timber");
    using (var output = File.Create(path)) {
      payload.CopyTo(output);
    }
    var bytes = File.ReadAllBytes(path);
    OciDescriptor descriptor;
    try {
      descriptor = PushBlobWithRetry(path, publishedFileId);
    } catch (Exception exception) when (exception is PayloadCacheException or InvalidDataException) {
      _writeFailures++;
      Console.Error.WriteLine(
          $"Payload cache write failed for {publishedFileId}; continuing without caching it: {exception.Message}");
      return false;
    }

    // Do not retire the previous cached version until the replacement blob is safely stored.
    foreach (var obsolete in _catalog.Keys.Where(key => key.StartsWith($"{publishedFileId}/", StringComparison.Ordinal)
        && key != entryName).ToList()) {
      _catalog.Remove(obsolete);
    }
    var shard = CreateShardTag(publishedFileId);
    _catalog[entryName] = new CatalogEntry(
        shard, descriptor.Digest, bytes.LongLength,
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    _dirtyShards.Add(shard);
    _catalogDirty = true;
    return true;
  }

  OciDescriptor PushBlobWithRetry(string path, string publishedFileId) {
    var result = RunRegistryWriteWithRetry([
        "blob", "push", "--media-type", PayloadMediaType, "--descriptor", _repository, path,
    ], $"payload cache write for {publishedFileId}");
    var descriptor = JsonSerializer.Deserialize<OciDescriptor>(result.Output)
        ?? throw new InvalidDataException("oras blob push returned no OCI descriptor.");
    if (descriptor.Size != new FileInfo(path).Length || string.IsNullOrWhiteSpace(descriptor.Digest)) {
      throw new InvalidDataException("oras blob push returned an inconsistent OCI descriptor.");
    }
    return descriptor;
  }

  /// <summary>
  /// Tries to publish pending cache manifests without allowing a registry failure to discard map analysis results.
  /// </summary>
  public bool TryFlush() {
    if (!_catalogDirty) {
      return true;
    }
    try {
      var emptyConfigPath = Path.Combine(_workDirectory, "empty-config.json");
      File.WriteAllText(emptyConfigPath, "{}");
      var configResult = RunRegistryWriteWithRetry([
          "blob", "push", "--media-type", ConfigMediaType, "--descriptor", _repository, emptyConfigPath,
      ], "payload cache config write");
      var config = JsonSerializer.Deserialize<OciDescriptor>(configResult.Output)
          ?? throw new InvalidDataException("oras blob push returned no config descriptor.");

      foreach (var shard in _dirtyShards.Order()) {
        var layers = _catalog.Where(pair => pair.Value.Shard == shard).OrderBy(pair => pair.Key)
            .Select(pair => new OciDescriptor(
                PayloadMediaType, pair.Value.Digest, pair.Value.Size,
                new Dictionary<string, string> {
                    ["org.opencontainers.image.title"] = pair.Key,
                    ["com.ihsoft.timberborn.sha256"] = pair.Value.Sha256,
                })).ToList();
        var manifest = new OciManifest(2, ManifestMediaType, ArtifactType, config, layers);
        var manifestPath = Path.Combine(_workDirectory, $"{shard}.manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        RunRegistryWriteWithRetry(
            ["manifest", "push", $"{_repository}:{shard}", manifestPath], $"payload cache manifest {shard}");
        Console.WriteLine($"Published payload cache manifest {shard} with {layers.Count} map versions.");
      }

      var catalogPath = Path.Combine(_workDirectory, "catalog.json");
      File.WriteAllText(catalogPath, JsonSerializer.Serialize(_catalog));
      RunRegistryWriteWithRetry([
          "push", $"{_repository}:{CatalogTag}", "--artifact-type", ArtifactType,
          $"{Path.GetFileName(catalogPath)}:{CatalogMediaType}",
      ], "payload cache catalog write", _workDirectory);
      Console.WriteLine($"Published payload cache catalog with {_catalog.Count} map versions.");
      _dirtyShards.Clear();
      _catalogDirty = false;
      return true;
    } catch (PayloadCacheException exception) {
      _flushFailures++;
      Console.Error.WriteLine(
          $"Payload cache publication failed; continuing with map metadata output: {exception.Message}");
      return false;
    }
  }

  public void Dispose() {
  }

  public static string CreateEntryName(string publishedFileId, string? updatedAtUtc) {
    var version = DateTimeOffset.TryParse(updatedAtUtc, out var timestamp)
        ? timestamp.ToUnixTimeSeconds().ToString()
        : throw new InvalidDataException($"Workshop item {publishedFileId} has no valid update timestamp.");
    return $"{publishedFileId}/{version}.timber";
  }

  public static string CreateShardTag(string publishedFileId) {
    var shard = ulong.Parse(publishedFileId) % ShardCount;
    return $"shard-{shard:D3}";
  }

  static OrasCommandResult RunOras(
      IReadOnlyList<string> arguments, bool allowNotFound = false, string? workingDirectory = null) {
    var startInfo = new ProcessStartInfo("oras") {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
    };
    foreach (var argument in arguments) {
      startInfo.ArgumentList.Add(argument);
    }
    Process process;
    try {
      process = Process.Start(startInfo)
          ?? throw new PayloadCacheException("Could not start the oras command.");
    } catch (Exception exception) when (exception is not PayloadCacheException) {
      throw new PayloadCacheException("Could not start the oras command.", exception);
    }
    using (process) {
      var output = process.StandardOutput.ReadToEnd();
      var error = process.StandardError.ReadToEnd();
      process.WaitForExit();
      if (process.ExitCode == 0) {
        return new OrasCommandResult(true, output);
      }
      if (allowNotFound && (error.Contains("not found", StringComparison.OrdinalIgnoreCase)
          || error.Contains("404", StringComparison.OrdinalIgnoreCase)
          || error.Contains("manifest unknown", StringComparison.OrdinalIgnoreCase)
          || error.Contains("name unknown", StringComparison.OrdinalIgnoreCase))) {
        return new OrasCommandResult(false, output);
      }
      throw new PayloadCacheException(
          $"oras {arguments[0]} failed with exit code {process.ExitCode}: {(error + output).Trim()}");
    }
  }

  static OrasCommandResult RunRegistryWriteWithRetry(
      IReadOnlyList<string> arguments, string operation, string? workingDirectory = null) {
    for (var attempt = 0; ; attempt++) {
      try {
        return RunOras(arguments, workingDirectory: workingDirectory);
      } catch (PayloadCacheException exception) when (attempt < RegistryWriteRetryDelays.Length) {
        var delay = RegistryWriteRetryDelays[attempt];
        Console.Error.WriteLine(
            $"{operation} failed: {exception.Message}; retrying in {delay.TotalSeconds:0} seconds "
            + $"({attempt + 1} / {RegistryWriteRetryDelays.Length}).");
        Thread.Sleep(delay);
      }
    }
  }

}

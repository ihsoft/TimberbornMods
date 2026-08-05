#nullable enable
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

sealed class OciPayloadCache : IDisposable {
  const ulong ShardCount = 100;
  const string CatalogTag = "catalog-v1";
  const string ArtifactType = "application/vnd.timberborn.workshop-payload-cache.v1";
  const string PayloadMediaType = "application/vnd.timberborn.map-payload.v1+zip";
  const string CatalogMediaType = "application/vnd.timberborn.workshop-payload-catalog.v1+json";
  const string ManifestMediaType = "application/vnd.oci.image.manifest.v1+json";
  const string ConfigMediaType = "application/vnd.oci.empty.v1+json";
  readonly string _repository;
  readonly string _workDirectory;
  readonly Dictionary<string, CatalogEntry> _catalog;
  readonly HashSet<string> _dirtyShards = [];
  bool _catalogDirty;

  OciPayloadCache(string repository, string workDirectory, Dictionary<string, CatalogEntry> catalog) {
    _repository = repository;
    _workDirectory = workDirectory;
    _catalog = catalog;
  }

  public int Count => _catalog.Count;

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
    if (entry.Size < 0 || (ulong)entry.Size > maximumBytes) {
      throw new InvalidDataException($"Cached payload {entryName} has an unexpected size {entry.Size}.");
    }
    var path = Path.Combine(_workDirectory, $"read-{Guid.NewGuid():N}.timber");
    RunOras(["blob", "fetch", "--output", path, $"{_repository}@{entry.Digest}"]);
    var bytes = File.ReadAllBytes(path);
    var actualHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    if (bytes.LongLength != entry.Size || !string.Equals(entry.Sha256, actualHash, StringComparison.OrdinalIgnoreCase)) {
      throw new InvalidDataException($"Cached payload {entryName} failed size or SHA-256 validation.");
    }
    return bytes;
  }

  public void Write(string publishedFileId, string? updatedAtUtc, Stream payload) {
    var entryName = CreateEntryName(publishedFileId, updatedAtUtc);
    foreach (var obsolete in _catalog.Keys.Where(key => key.StartsWith($"{publishedFileId}/", StringComparison.Ordinal)
        && key != entryName).ToList()) {
      _catalog.Remove(obsolete);
    }
    var path = Path.Combine(_workDirectory, $"write-{Guid.NewGuid():N}.timber");
    using (var output = File.Create(path)) {
      payload.CopyTo(output);
    }
    var bytes = File.ReadAllBytes(path);
    var descriptorResult = RunOras([
      "blob", "push", "--media-type", PayloadMediaType, "--descriptor", _repository, path,
    ]);
    var descriptor = JsonSerializer.Deserialize<OciDescriptor>(descriptorResult.Output)
        ?? throw new InvalidDataException("oras blob push returned no OCI descriptor.");
    if (descriptor.Size != bytes.LongLength || string.IsNullOrWhiteSpace(descriptor.Digest)) {
      throw new InvalidDataException("oras blob push returned an inconsistent OCI descriptor.");
    }
    var shard = CreateShardTag(publishedFileId);
    _catalog[entryName] = new CatalogEntry(
        shard, descriptor.Digest, bytes.LongLength,
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    _dirtyShards.Add(shard);
    _catalogDirty = true;
  }

  public void Flush() {
    if (!_catalogDirty) {
      return;
    }
    var emptyConfigPath = Path.Combine(_workDirectory, "empty-config.json");
    File.WriteAllText(emptyConfigPath, "{}");
    var configResult = RunOras([
      "blob", "push", "--media-type", ConfigMediaType, "--descriptor", _repository, emptyConfigPath,
    ]);
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
      RunOras(["manifest", "push", $"{_repository}:{shard}", manifestPath]);
      Console.WriteLine($"Published payload cache manifest {shard} with {layers.Count} map versions.");
    }

    var catalogPath = Path.Combine(_workDirectory, "catalog.json");
    File.WriteAllText(catalogPath, JsonSerializer.Serialize(_catalog));
    RunOras([
      "push", $"{_repository}:{CatalogTag}", "--artifact-type", ArtifactType,
      $"{Path.GetFileName(catalogPath)}:{CatalogMediaType}",
    ], workingDirectory: _workDirectory);
    Console.WriteLine($"Published payload cache catalog with {_catalog.Count} map versions.");
    _dirtyShards.Clear();
    _catalogDirty = false;
  }

  public void Dispose() {
  }

  internal static string CreateEntryName(string publishedFileId, string? updatedAtUtc) {
    var version = DateTimeOffset.TryParse(updatedAtUtc, out var timestamp)
        ? timestamp.ToUnixTimeSeconds().ToString()
        : throw new InvalidDataException($"Workshop item {publishedFileId} has no valid update timestamp.");
    return $"{publishedFileId}/{version}.timber";
  }

  internal static string CreateShardTag(string publishedFileId) {
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
}

sealed class PayloadCacheException(string message, Exception? innerException = null)
    : Exception(message, innerException);

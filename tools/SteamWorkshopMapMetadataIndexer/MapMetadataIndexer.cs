// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;
using Steamworks;

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed class MapMetadataIndexer {
  const uint AppId = 1062090;
  static readonly TimeSpan SteamReconnectShutdownDelay = TimeSpan.FromSeconds(1);
  static readonly TimeSpan[] SteamReconnectRetryDelays = [TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40)];

  sealed record MapItem(string PublishedFileId, string? UpdatedAtUtc, long PayloadSizeBytes);

  sealed record CachedAnalysisResult(MapArchiveAnalysis? Analysis, Exception? Error);

  sealed record MapMetadataRecord(
      [property: JsonPropertyName("published_file_id")] string PublishedFileId,
      [property: JsonPropertyName("source_updated_at_utc")] string? SourceUpdatedAtUtc,
      [property: JsonPropertyName("analysis_version")] int AnalysisVersion,
      [property: JsonPropertyName("map_width")] int MapWidth,
      [property: JsonPropertyName("map_height")] int MapHeight,
      [property: JsonPropertyName("classifications")] IReadOnlyDictionary<string, JsonElement>? Classifications,
      [property: JsonPropertyName("collection_state")] string CollectionState,
      [property: JsonPropertyName("analysis_error")] string? AnalysisError);

  sealed class UnsupportedMapPayloadException(string message, Exception? innerException = null)
      : Exception(message, innerException);

  sealed class SteamPayloadTransientException(EResult result)
      : Exception($"Steam Workshop request returned transient result {result}.") {
    public EResult Result { get; } = result;
  }

  sealed class SteamPayloadRequestException(string operation, EResult result)
      : Exception($"{operation} returned {result}.") {
    public EResult Result { get; } = result;
  }

  sealed class SteamServerConnectException(EResult result)
      : Exception($"Anonymous server login failed: {result}.") {
    public EResult Result { get; } = result;
  }

  sealed class SteamDownloadSession(
      MapMetadataIndexerOptions options, string workshopDirectory, SteamRequestPacer requestPacer) {
    int _downloadRequestsSinceLogin;

    public void PrepareDownloadRequest() {
      if (SteamReconnectPolicy.ShouldReconnect(
          _downloadRequestsSinceLogin, options.SteamReconnectAfterDownloads)) {
        Console.WriteLine(
            $"Reconnecting anonymous Steam session after {_downloadRequestsSinceLogin} download requests.");
        ReconnectAnonymously(options.RequestTimeout);
        InitializeWorkshopDirectory(workshopDirectory);
        requestPacer.ResetForNewSession();
        _downloadRequestsSinceLogin = 0;
        Console.WriteLine("Anonymous Steam session reconnected.");
      }
      _downloadRequestsSinceLogin++;
    }
  }

  readonly MapArchiveAnalyzer _archiveAnalyzer = new();

  /// <summary>Refreshes exact map metadata from cached and anonymously downloaded Workshop payloads.</summary>
  public int Run(MapMetadataIndexerOptions options) {
    var maps = ReadMaps(options.Snapshot);
    var previousById = ReadRecords(options.PreviousResults).ToDictionary(record => record.PublishedFileId);
    var outputById = maps
        .Where(map => previousById.ContainsKey(map.PublishedFileId))
        .ToDictionary(map => map.PublishedFileId, map => previousById[map.PublishedFileId]);
    var processedThisRun = 0;
    var downloadedThisRun = 0;
    using var payloadCache = OciPayloadCache.CreateFromEnvironment(options.MaxAnalysisParallelism);
    payloadCache?.PruneExcept(maps.Select(map => map.PublishedFileId).ToHashSet(StringComparer.Ordinal));
    var refreshCandidates = maps
        .Where(map => NeedsRefresh(map, previousById.GetValueOrDefault(map.PublishedFileId)))
        .ToList();
    var cachedCandidates = payloadCache is null
        ? []
        : refreshCandidates.Where(map => payloadCache.Contains(map.PublishedFileId, map.UpdatedAtUtc))
            .OrderBy(map => ParseTimestamp(map.UpdatedAtUtc)).ToList();
    var cachedIds = cachedCandidates.Select(map => map.PublishedFileId).ToHashSet();
    var refreshDownloads = refreshCandidates.Where(map => !cachedIds.Contains(map.PublishedFileId))
        .OrderByDescending(map => ParseTimestamp(map.UpdatedAtUtc)).ToList();
    var cacheFillDownloads = new List<MapItem>();
    if (payloadCache is not null) {
      cacheFillDownloads = maps.Where(map => {
        var previous = previousById.GetValueOrDefault(map.PublishedFileId);
        return MapPayloadCachePolicy.ShouldPopulate(
            previous?.CollectionState,
            NeedsRefresh(map, previous),
            payloadCache.Contains(map.PublishedFileId, map.UpdatedAtUtc));
      })
          .OrderByDescending(map => ParseTimestamp(map.UpdatedAtUtc)).ToList();
    }
    var downloadCandidates = refreshDownloads.Concat(cacheFillDownloads)
        .Take(options.MaxDownloadItems == 0 ? int.MaxValue : options.MaxDownloadItems).ToList();
    var candidates = cachedCandidates.Concat(downloadCandidates).ToList();
    var deadline = options.TimeBudget == TimeSpan.Zero
        ? DateTimeOffset.MaxValue
        : DateTimeOffset.UtcNow.Add(options.TimeBudget);

    if (cachedCandidates.Count > 0) {
      Console.WriteLine(
          $"Analyzing {cachedCandidates.Count} cached map payloads with "
          + $"up to {options.MaxAnalysisParallelism} workers.");
      var cachedResults = new ConcurrentDictionary<string, CachedAnalysisResult>(StringComparer.Ordinal);
      var cachedProgress = 0;
      Parallel.ForEach(cachedCandidates, new ParallelOptions {
        MaxDegreeOfParallelism = options.MaxAnalysisParallelism,
      }, map => {
        if (DateTimeOffset.UtcNow >= deadline) {
          return;
        }
        try {
          var payload = payloadCache!.TryRead(map.PublishedFileId, map.UpdatedAtUtc, options.MaxDownloadBytes)
              ?? throw new InvalidDataException("Payload cache catalog entry disappeared before analysis.");
          cachedResults[map.PublishedFileId] = new CachedAnalysisResult(
              AnalyzePayload(new MemoryStream(payload, writable: false)), null);
        } catch (UnsupportedMapPayloadException exception) {
          cachedResults[map.PublishedFileId] = new CachedAnalysisResult(null, exception);
        } catch (Exception exception) {
          Console.Error.WriteLine($"Cached map payload failed for {map.PublishedFileId}: {exception.Message}");
          cachedResults[map.PublishedFileId] = new CachedAnalysisResult(null, exception);
        } finally {
          var progress = Interlocked.Increment(ref cachedProgress);
          Console.WriteLine($"Cached map metadata progress: {progress} / {cachedCandidates.Count} selected maps.");
        }
      });
      foreach (var map in cachedCandidates) {
        if (!cachedResults.TryGetValue(map.PublishedFileId, out var result)) {
          continue;
        }
        if (result.Analysis is not null) {
          outputById[map.PublishedFileId] = CreateFetchedRecord(map, result.Analysis);
        } else if (result.Error is UnsupportedMapPayloadException unsupported) {
          Console.Error.WriteLine($"Map payload unsupported for {map.PublishedFileId}: {unsupported.Message}");
          outputById[map.PublishedFileId] = CreateUnsupportedRecord(map, unsupported);
        } else {
          PreserveFailedRecord(map, previousById, outputById);
        }
        processedThisRun++;
      }
    }

    if (StopRequestMonitor.IsStopRequested(options.StopRequestFile)) {
      Console.WriteLine("Graceful stop requested before the Steam payload pass.");
    } else if (downloadCandidates.Count > 0 && DateTimeOffset.UtcNow < deadline) {
      Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
      Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());
      if (!Packsize.Test() || !DllCheck.Test()) {
        Console.Error.WriteLine("Steamworks.NET native library validation failed.");
        return 2;
      }

      try {
        InitializeGameServer();
      } catch (Exception exception) {
        Console.Error.WriteLine(exception.Message);
        return 3;
      }

      try {
        ConnectAnonymously(options.RequestTimeout);
        var workshopDirectory = Path.GetFullPath(options.WorkshopDirectory);
        Directory.CreateDirectory(workshopDirectory);
        InitializeWorkshopDirectory(workshopDirectory);

        Console.WriteLine(
            $"Anonymous Steam session connected; reading {downloadCandidates.Count} download-required map payloads.");
        var requestPacer = new SteamRequestPacer(
            Thread.Sleep, normalModeDelay: options.RequestDelay, slowModeDelay: options.SlowModeDelay);
        var downloadSession = new SteamDownloadSession(options, workshopDirectory, requestPacer);
        for (var index = 0; index < downloadCandidates.Count; index++) {
          if (StopRequestMonitor.IsStopRequested(options.StopRequestFile)) {
            Console.WriteLine($"Graceful stop requested after {index} / {downloadCandidates.Count} Steam maps.");
            break;
          }
          if (DateTimeOffset.UtcNow >= deadline) {
            Console.WriteLine($"Time budget reached after {index} / {downloadCandidates.Count} Steam maps.");
            break;
          }

          var map = downloadCandidates[index];
          try {
            var (analysis, downloaded) = ReadAndAnalyzeMapWithTransientRetry(
                map, options, payloadCache, requestPacer, downloadSession);
            if (downloaded) {
              downloadedThisRun++;
            }
            outputById[map.PublishedFileId] = CreateFetchedRecord(map, analysis);
          } catch (UnsupportedMapPayloadException exception) {
            Console.Error.WriteLine($"Map payload unsupported for {map.PublishedFileId}: {exception.Message}");
            outputById[map.PublishedFileId] = CreateUnsupportedRecord(map, exception);
          } catch (Exception exception) {
            Console.Error.WriteLine($"Map payload request failed for {map.PublishedFileId}: {exception.Message}");
            PreserveFailedRecord(map, previousById, outputById);
            break;
          }
          processedThisRun++;
          Console.WriteLine($"Steam map metadata progress: {index + 1} / {downloadCandidates.Count} selected maps.");
        }
      } finally {
        SteamGameServer.LogOff();
        GameServer.Shutdown();
      }
    }

    static MapMetadataRecord CreateFetchedRecord(MapItem map, MapArchiveAnalysis analysis) {
      return new MapMetadataRecord(
          map.PublishedFileId, map.UpdatedAtUtc, MapArchiveAnalyzer.AnalysisVersion,
          analysis.Width, analysis.Height, analysis.Classifications, "fetched", null);
    }

    static MapMetadataRecord CreateUnsupportedRecord(MapItem map, Exception exception) {
      return new MapMetadataRecord(
          map.PublishedFileId, map.UpdatedAtUtc, MapArchiveAnalyzer.AnalysisVersion,
          0, 0, null, "unsupported", exception.Message);
    }

    static void PreserveFailedRecord(
        MapItem map, IReadOnlyDictionary<string, MapMetadataRecord> previousById,
        IDictionary<string, MapMetadataRecord> outputById) {
      var previous = previousById.GetValueOrDefault(map.PublishedFileId);
      if (previous is not null) {
        outputById[map.PublishedFileId] = NeedsRefresh(map, previous)
            ? previous with { CollectionState = "stale" }
            : previous;
      }
    }

    payloadCache?.TryFlush();
    WriteRecords(options.Output, maps, outputById);
    var upToDate = maps.Count(map => outputById.TryGetValue(map.PublishedFileId, out var record)
        && !NeedsRefresh(map, record));
    var stale = outputById.Values.Count(record => record.CollectionState == "stale");
    Console.WriteLine(
        $"Wrote {outputById.Count} map metadata records; selected {candidates.Count}, "
        + $"processed this run {processedThisRun}, downloaded {downloadedThisRun}, up-to-date {upToDate}, "
        + $"remaining refresh {maps.Count - upToDate}, stale {stale}, "
        + $"payload cache pruned maps {payloadCache?.PrunedMaps ?? 0}, "
        + $"payload cache pruned versions {payloadCache?.PrunedVersions ?? 0}, "
        + $"payload cache write failures {payloadCache?.WriteFailures ?? 0}, "
        + $"payload cache publication failures {payloadCache?.FlushFailures ?? 0}.");
    return 0;
  }

  (MapArchiveAnalysis Analysis, bool Downloaded) ReadAndAnalyzeMapWithTransientRetry(
      MapItem map, MapMetadataIndexerOptions options, OciPayloadCache? payloadCache, SteamRequestPacer requestPacer,
      SteamDownloadSession downloadSession) {
    var cachedPayload = payloadCache?.TryRead(map.PublishedFileId, map.UpdatedAtUtc, options.MaxDownloadBytes);
    if (cachedPayload is not null) {
      return (AnalyzePayload(new MemoryStream(cachedPayload, writable: false)), false);
    }
    ValidateDeclaredPayloadSize(map, options.MaxDownloadBytes);
    var retryDelays = new[] { TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(40) };
    var delayAlreadyApplied = TimeSpan.Zero;
    for (var attempt = 0; ; attempt++) {
      downloadSession.PrepareDownloadRequest();
      requestPacer.WaitBeforeRequest(delayAlreadyApplied);
      try {
        var analysis = DownloadAndAnalyzeMap(map, options, payloadCache);
        requestPacer.RecordSuccessfulRequest();
        return (analysis, true);
      } catch (UnsupportedMapPayloadException) {
        requestPacer.RecordSuccessfulRequest();
        throw;
      } catch (Exception exception) when (GetTransientFailureReason(exception, requestPacer) is not null) {
        var reason = GetTransientFailureReason(exception, requestPacer)!;
        requestPacer.RecordTransientFailure(reason);
        if (attempt >= retryDelays.Length) {
          throw;
        }
        var retryDelay = retryDelays[attempt];
        Console.WriteLine(
            $"Steam request failed transiently with {reason} for {map.PublishedFileId}; "
            + $"retrying in {retryDelay.TotalSeconds:0} seconds ({attempt + 1} / {retryDelays.Length}).");
        Thread.Sleep(retryDelay);
        delayAlreadyApplied = retryDelay;
      }
    }
  }

  static string? GetTransientFailureReason(Exception exception, SteamRequestPacer requestPacer) {
    return exception switch {
      SteamPayloadTransientException transient => transient.Result.ToString(),
      SteamPayloadRequestException failure
          when requestPacer.ShouldTreatAsTransient(failure.Result.ToString()) => failure.Result.ToString(),
      TimeoutException => "Timeout",
      _ => null,
    };
  }

  MapArchiveAnalysis DownloadAndAnalyzeMap(
      MapItem map, MapMetadataIndexerOptions options, OciPayloadCache? payloadCache) {
    var itemId = new PublishedFileId_t(ulong.Parse(map.PublishedFileId));
    var completed = false;
    DownloadItemResult_t response = default;
    using var callback = Callback<DownloadItemResult_t>.CreateGameServer(result => {
      if (result.m_nPublishedFileId == itemId) {
        response = result;
        completed = true;
      }
    });
    if (!SteamGameServerUGC.DownloadItem(itemId, false)) {
      throw new InvalidOperationException("Steam rejected the anonymous Workshop download request.");
    }
    WaitForCallback(() => completed, "Workshop item download", options.RequestTimeout);
    ThrowIfTransient(response.m_eResult);
    if (response.m_eResult != EResult.k_EResultOK) {
      throw new SteamPayloadRequestException("Workshop download", response.m_eResult);
    }
    if (response.m_unAppID.m_AppId != AppId) {
      throw new InvalidOperationException($"Workshop download returned AppID {response.m_unAppID.m_AppId}.");
    }
    if (!SteamGameServerUGC.GetItemInstallInfo(itemId, out var sizeOnDisk, out var folder, 4096, out _)) {
      throw new InvalidOperationException("GetItemInstallInfo returned false after download.");
    }
    if (sizeOnDisk > options.MaxDownloadBytes) {
      throw new UnsupportedMapPayloadException(
          $"Downloaded payload is {sizeOnDisk} bytes; limit is {options.MaxDownloadBytes} bytes.");
    }

    try {
      var mapFiles = Directory.EnumerateFiles(folder, "*.timber", SearchOption.AllDirectories).ToArray();
      if (mapFiles.Length != 1) {
        throw new InvalidDataException(
            $"Payload contains {mapFiles.Length} .timber files; expected exactly one.");
      }
      using var payload = File.OpenRead(mapFiles[0]);
      payloadCache?.TryWrite(map.PublishedFileId, map.UpdatedAtUtc, payload);
      payload.Position = 0;
      return AnalyzePayload(payload);
    } catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException
        or JsonException or IOException or UnauthorizedAccessException) {
      throw new UnsupportedMapPayloadException(exception.Message, exception);
    }
  }

  static void ValidateDeclaredPayloadSize(MapItem map, ulong maxDownloadBytes) {
    if (map.PayloadSizeBytes < 0) {
      throw new UnsupportedMapPayloadException(
          $"Workshop declared an invalid payload size: {map.PayloadSizeBytes}.");
    }
    if ((ulong) map.PayloadSizeBytes > maxDownloadBytes) {
      throw new UnsupportedMapPayloadException(
          $"Declared payload is {map.PayloadSizeBytes} bytes; limit is {maxDownloadBytes} bytes.");
    }
  }

  MapArchiveAnalysis AnalyzePayload(Stream payload) {
    try {
      using (payload) {
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        return _archiveAnalyzer.Analyze(archive);
      }
    } catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException
        or JsonException or IOException or UnauthorizedAccessException) {
      throw new UnsupportedMapPayloadException(exception.Message, exception);
    }
  }

  static void ThrowIfTransient(EResult result) {
    if (result is EResult.k_EResultBusy or EResult.k_EResultNoConnection) {
      throw new SteamPayloadTransientException(result);
    }
  }

  static List<MapItem> ReadMaps(string path) {
    using var stream = OpenRead(path);
    using var reader = new StreamReader(stream);
    var maps = new List<MapItem>();
    while (reader.ReadLine() is { } line) {
      if (string.IsNullOrWhiteSpace(line)) {
        continue;
      }
      using var document = JsonDocument.Parse(line);
      var root = document.RootElement;
      if (HasTag(root, "Map")) {
        maps.Add(new MapItem(
            root.GetProperty("published_file_id").GetString()
                ?? throw new InvalidDataException("Workshop item has no published_file_id."),
            GetOptionalString(root, "updated_at_utc"),
            root.GetProperty("payload_size_bytes").GetInt64()));
      }
    }
    return maps;
  }

  static bool HasTag(JsonElement item, string expectedTag) {
    return item.TryGetProperty("tags", out var tags)
        && tags.ValueKind == JsonValueKind.Array
        && tags.EnumerateArray().Any(tag => tag.ValueKind == JsonValueKind.String
        && string.Equals(tag.GetString(), expectedTag, StringComparison.OrdinalIgnoreCase));
  }

  static List<MapMetadataRecord> ReadRecords(string? path) {
    if (path is null || !File.Exists(path)) {
      return [];
    }
    using var stream = OpenRead(path);
    using var reader = new StreamReader(stream);
    var records = new List<MapMetadataRecord>();
    while (reader.ReadLine() is { } line) {
      if (!string.IsNullOrWhiteSpace(line)) {
        records.Add(JsonSerializer.Deserialize<MapMetadataRecord>(line)
            ?? throw new InvalidDataException("Map metadata record could not be deserialized."));
      }
    }
    return records;
  }

  static Stream OpenRead(string path) {
    var stream = File.OpenRead(path);
    return path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
        ? new GZipStream(stream, CompressionMode.Decompress)
        : stream;
  }

  static string? GetOptionalString(JsonElement element, string propertyName) {
    return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
        ? property.GetString()
        : null;
  }

  static bool NeedsRefresh(MapItem map, MapMetadataRecord? previous) {
    if (previous is not null
        && previous.CollectionState == "unsupported"
        && previous.SourceUpdatedAtUtc == map.UpdatedAtUtc
        && previous.AnalysisVersion == MapArchiveAnalyzer.AnalysisVersion) {
      return false;
    }
    return previous is null || previous.CollectionState == "stale"
        || previous.SourceUpdatedAtUtc != map.UpdatedAtUtc
        || previous.AnalysisVersion != MapArchiveAnalyzer.AnalysisVersion
        || previous.MapWidth < 1 || previous.MapHeight < 1
        || previous.Classifications?.ContainsKey(ForestDensityClassifier.FeatureKey) != true
        || previous.Classifications?.ContainsKey(WaterFormClassifier.FeatureKey) != true
        || previous.Classifications?.ContainsKey(SettlementSpaceClassifier.FeatureKey) != true
        || previous.Classifications?.ContainsKey(IslandClassifier.FeatureKey) != true
        || previous.Classifications?.ContainsKey(CanyonClassifier.FeatureKey) != true
        || previous.Classifications?.ContainsKey(MountainClassifier.FeatureKey) != true;
  }

  static DateTimeOffset ParseTimestamp(string? value) {
    return DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : DateTimeOffset.MinValue;
  }

  static void ConnectAnonymously(TimeSpan timeout) {
    var connected = false;
    var connectFailure = EResult.k_EResultNone;
    using var connectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(_ => connected = true);
    using var failedCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(
        result => connectFailure = result.m_eResult);
    SteamGameServer.LogOnAnonymous();
    WaitForCallback(() => connected || connectFailure != EResult.k_EResultNone, "anonymous server login", timeout);
    if (!connected) {
      throw new SteamServerConnectException(connectFailure);
    }
  }

  static void ReconnectAnonymously(TimeSpan timeout) {
    SteamGameServer.LogOff();
    GameServer.Shutdown();
    Console.WriteLine(
        $"Steam game server stopped; waiting {SteamReconnectShutdownDelay.TotalSeconds:0} second before restart.");
    Thread.Sleep(SteamReconnectShutdownDelay);

    for (var attempt = 0; ; attempt++) {
      InitializeGameServer();
      try {
        ConnectAnonymously(timeout);
        return;
      } catch (SteamServerConnectException exception)
          when (exception.Result == EResult.k_EResultNoConnection
              && attempt < SteamReconnectRetryDelays.Length) {
        GameServer.Shutdown();
        var retryDelay = SteamReconnectRetryDelays[attempt];
        Console.WriteLine(
            $"Anonymous Steam reconnect failed transiently with {exception.Result}; "
            + $"retrying in {retryDelay.TotalSeconds:0} seconds "
            + $"({attempt + 1} / {SteamReconnectRetryDelays.Length}).");
        Thread.Sleep(retryDelay);
      } catch {
        GameServer.Shutdown();
        throw;
      }
    }
  }

  static void InitializeGameServer() {
    var initResult = GameServer.InitEx(
        0, 0, 0, EServerMode.eServerModeNoAuthentication, "workshop-map-metadata-indexer", out var initError);
    if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK) {
      throw new InvalidOperationException(
          $"Steam game-server initialization failed: {initResult}: {initError}");
    }
  }

  static void InitializeWorkshopDirectory(string workshopDirectory) {
    if (!SteamGameServerUGC.BInitWorkshopForGameServer(new DepotId_t(AppId), workshopDirectory)) {
      throw new InvalidOperationException("Steam rejected the explicit game-server Workshop directory.");
    }
  }

  static void WaitForCallback(Func<bool> completed, string operation, TimeSpan timeout) {
    var deadline = DateTime.UtcNow.Add(timeout);
    while (!completed() && DateTime.UtcNow < deadline) {
      GameServer.RunCallbacks();
      Thread.Sleep(100);
    }
    if (!completed()) {
      throw new TimeoutException($"Timed out waiting for {operation}.");
    }
  }

  static void WriteRecords(
      string path, IReadOnlyCollection<MapItem> maps, IReadOnlyDictionary<string, MapMetadataRecord> outputById) {
    var directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (directory is not null) {
      Directory.CreateDirectory(directory);
    }
    using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
    foreach (var map in maps) {
      if (outputById.TryGetValue(map.PublishedFileId, out var record)) {
        writer.WriteLine(JsonSerializer.Serialize(record));
      }
    }
  }

}

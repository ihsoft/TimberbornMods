using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Steamworks;

const uint appId = 1062090;
var options = Options.Parse(args);
var maps = ReadMaps(options.Snapshot);
var previousById = ReadGalleryRecords(options.PreviousResults)
    .ToDictionary(record => record.PublishedFileId);
var refreshBefore = DateTimeOffset.UtcNow.AddDays(-options.RefreshAfterDays);
var candidates = maps
    .Where(map => NeedsRefresh(map, previousById.GetValueOrDefault(map.PublishedFileId), refreshBefore))
    .OrderBy(map => CandidateKey(map, previousById.GetValueOrDefault(map.PublishedFileId)).Priority)
    .ThenBy(map => CandidateKey(map, previousById.GetValueOrDefault(map.PublishedFileId)).Timestamp)
    .Take(options.MaxItems == 0 ? int.MaxValue : options.MaxItems)
    .ToList();
var selectedIds = candidates.Select(map => map.PublishedFileId).ToHashSet();
var outputById = new Dictionary<string, GalleryRecord>();
foreach (var map in maps) {
  var previous = previousById.GetValueOrDefault(map.PublishedFileId);
  if (!selectedIds.Contains(map.PublishedFileId) && previous is not null) {
    outputById[map.PublishedFileId] = previous with { CollectionState = "reused" };
  }
}

if (candidates.Count > 0) {
  Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
  Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
  if (!Packsize.Test() || !DllCheck.Test()) {
    Console.Error.WriteLine("Steamworks.NET native library validation failed.");
    return 2;
  }

  var initResult = GameServer.InitEx(
      0, 0, 0, EServerMode.eServerModeNoAuthentication, "workshop-gallery-indexer", out var initError);
  if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK) {
    Console.Error.WriteLine($"Steam game-server initialization failed: {initResult}: {initError}");
    return 3;
  }

  try {
    ConnectAnonymously();
    Console.WriteLine(
        $"Anonymous Steam session connected; querying {candidates.Count} maps in batches of {options.BatchSize}.");
    Console.Out.Flush();
    var checkedAt = DateTimeOffset.UtcNow.ToString("O");
    var successfulBatches = 0;
    var failedBatches = 0;
    var resolvedMaps = 0;
    for (var offset = 0; offset < candidates.Count; offset += options.BatchSize) {
      var batch = candidates.Skip(offset).Take(options.BatchSize).ToList();
      try {
        var galleryById = QueryAdditionalPreviews(batch);
        foreach (var map in batch) {
          if (galleryById.TryGetValue(map.PublishedFileId, out var urls)) {
            outputById[map.PublishedFileId] = CreateFetchedRecord(
                map, checkedAt, urls, options.MaxImagesPerMap);
            resolvedMaps++;
          } else {
            var previous = previousById.GetValueOrDefault(map.PublishedFileId);
            outputById[map.PublishedFileId] = previous is null
                ? CreateFailedRecord(map)
                : previous with { CollectionState = "stale" };
            Console.Error.WriteLine($"Steam returned no gallery details for {map.PublishedFileId}.");
          }
        }
        successfulBatches++;
      } catch (Exception exception) {
        failedBatches++;
        Console.Error.WriteLine(
            $"Gallery batch {offset / options.BatchSize + 1} failed: {exception.Message}");
        foreach (var map in batch) {
          var previous = previousById.GetValueOrDefault(map.PublishedFileId);
          outputById[map.PublishedFileId] = previous is null
              ? CreateFailedRecord(map)
              : previous with { CollectionState = "stale" };
        }
      }

      Console.WriteLine(
          $"Gallery progress: {Math.Min(offset + batch.Count, candidates.Count)} / {candidates.Count} maps; "
          + $"{successfulBatches} batches succeeded, {failedBatches} failed");
      Console.Out.Flush();
      if (offset + batch.Count < candidates.Count && options.DelayMilliseconds > 0) {
        Thread.Sleep(options.DelayMilliseconds);
      }
    }

    if (successfulBatches == 0 || resolvedMaps == 0) {
      throw new InvalidOperationException("No anonymous Workshop gallery batch returned usable map details.");
    }
  }
  finally {
    SteamGameServer.LogOff();
    GameServer.Shutdown();
  }
}

WriteGalleryRecords(options.Output, maps, outputById);
var fetched = outputById.Values.Count(record => record.CollectionState == "fetched");
var stale = outputById.Values.Count(record => record.CollectionState == "stale");
Console.WriteLine(
    $"Wrote {outputById.Count} gallery records; selected {candidates.Count}, fetched {fetched}, stale {stale}.");
Console.Out.Flush();
return 0;

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
    if (root.GetProperty("primary_category").GetString() != "map") {
      continue;
    }

    maps.Add(new MapItem(
        root.GetProperty("published_file_id").GetString()
            ?? throw new InvalidDataException("Workshop item has no published_file_id."),
        GetOptionalString(root, "updated_at_utc")));
  }
  return maps;
}

static List<GalleryRecord> ReadGalleryRecords(string? path) {
  if (path is null || !File.Exists(path)) {
    return [];
  }

  using var stream = OpenRead(path);
  using var reader = new StreamReader(stream);
  var records = new List<GalleryRecord>();
  while (reader.ReadLine() is { } line) {
    if (!string.IsNullOrWhiteSpace(line)) {
      records.Add(
          JsonSerializer.Deserialize<GalleryRecord>(line)
              ?? throw new InvalidDataException("Gallery record could not be deserialized."));
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

static bool NeedsRefresh(MapItem map, GalleryRecord? previous, DateTimeOffset refreshBefore) {
  if (previous is null || previous.CollectionState is "stale" or "deferred") {
    return true;
  }
  if (previous.SourceUpdatedAtUtc != map.UpdatedAtUtc) {
    return true;
  }
  return !DateTimeOffset.TryParse(previous.GalleryCheckedAtUtc, out var checkedAt) || checkedAt < refreshBefore;
}

static (int Priority, long Timestamp) CandidateKey(MapItem map, GalleryRecord? previous) {
  if (previous is not null
      && (previous.CollectionState is "stale" or "deferred"
          || previous.SourceUpdatedAtUtc != map.UpdatedAtUtc)) {
    return (0, 0);
  }
  if (previous is null) {
    return (1, -ParseTimestamp(map.UpdatedAtUtc).Ticks);
  }
  return (2, ParseTimestamp(previous.GalleryCheckedAtUtc).Ticks);
}

static DateTimeOffset ParseTimestamp(string? value) {
  return DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : DateTimeOffset.MinValue;
}

static void ConnectAnonymously() {
  var connected = false;
  var connectFailure = EResult.k_EResultNone;
  using var connectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(_ => connected = true);
  using var failedCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(
      result => connectFailure = result.m_eResult);
  SteamGameServer.LogOnAnonymous();
  WaitForCallback(() => connected || connectFailure != EResult.k_EResultNone, "anonymous server login");
  if (!connected) {
    throw new InvalidOperationException($"Anonymous server login failed: {connectFailure}.");
  }
}

static Dictionary<string, List<string>> QueryAdditionalPreviews(IReadOnlyCollection<MapItem> maps) {
  var ids = maps.Select(map => new PublishedFileId_t(ulong.Parse(map.PublishedFileId))).ToArray();
  var query = SteamGameServerUGC.CreateQueryUGCDetailsRequest(ids, (uint)ids.Length);
  if (query == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException("Could not create the anonymous Workshop details query.");
  }

  try {
    if (!SteamGameServerUGC.SetReturnAdditionalPreviews(query, true)) {
      throw new InvalidOperationException("Could not request additional Workshop previews.");
    }

    var completed = false;
    var ioFailureResult = false;
    SteamUGCQueryCompleted_t response = default;
    using var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
    callResult.Set(SteamGameServerUGC.SendQueryUGCRequest(query), (result, ioFailure) => {
      response = result;
      ioFailureResult = ioFailure;
      completed = true;
    });
    WaitForCallback(() => completed, "anonymous Workshop query");
    if (ioFailureResult || response.m_eResult != EResult.k_EResultOK) {
      throw new InvalidOperationException($"Anonymous Workshop query failed: {response.m_eResult}.");
    }

    var galleryById = new Dictionary<string, List<string>>();
    for (uint itemIndex = 0; itemIndex < response.m_unNumResultsReturned; itemIndex++) {
      if (!SteamGameServerUGC.GetQueryUGCResult(query, itemIndex, out var details)) {
        continue;
      }

      var publishedFileId = details.m_nPublishedFileId.m_PublishedFileId.ToString();
      var urls = new List<string>();
      var previewCount = SteamGameServerUGC.GetQueryUGCNumAdditionalPreviews(query, itemIndex);
      for (uint previewIndex = 0; previewIndex < previewCount; previewIndex++) {
        if (SteamGameServerUGC.GetQueryUGCAdditionalPreview(
                query, itemIndex, previewIndex, out var url, 4096, out _, 1024, out var previewType)
            && previewType == EItemPreviewType.k_EItemPreviewType_Image) {
          var normalized = NormalizeGalleryUrl(url);
          if (!urls.Contains(normalized, StringComparer.Ordinal)) {
            urls.Add(normalized);
          }
        }
      }
      galleryById[publishedFileId] = urls;
    }
    return galleryById;
  }
  finally {
    SteamGameServerUGC.ReleaseQueryUGCRequest(query);
  }
}

static string NormalizeGalleryUrl(string value) {
  var queryIndex = value.IndexOf('?');
  var baseUrl = queryIndex >= 0 ? value[..queryIndex] : value;
  return baseUrl + "?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true";
}

static GalleryRecord CreateFetchedRecord(
    MapItem map, string checkedAt, IReadOnlyCollection<string> urls, int maxImagesPerMap) {
  return new GalleryRecord(
      map.PublishedFileId,
      map.UpdatedAtUtc,
      checkedAt,
      urls.Take(maxImagesPerMap).ToList(),
      urls.Count,
      urls.Count > maxImagesPerMap,
      "fetched");
}

static GalleryRecord CreateFailedRecord(MapItem map) {
  return new GalleryRecord(
      map.PublishedFileId, map.UpdatedAtUtc, null, [], 0, false, "stale");
}

static void WriteGalleryRecords(
    string path, IReadOnlyCollection<MapItem> maps, IReadOnlyDictionary<string, GalleryRecord> outputById) {
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

static void WaitForCallback(Func<bool> completed, string operation) {
  var deadline = DateTime.UtcNow.AddSeconds(60);
  while (!completed() && DateTime.UtcNow < deadline) {
    GameServer.RunCallbacks();
    Thread.Sleep(100);
  }
  if (!completed()) {
    throw new TimeoutException($"Timed out waiting for {operation}.");
  }
}

sealed record MapItem(string PublishedFileId, string? UpdatedAtUtc);

sealed record GalleryRecord(
    [property: JsonPropertyName("published_file_id")] string PublishedFileId,
    [property: JsonPropertyName("source_updated_at_utc")] string? SourceUpdatedAtUtc,
    [property: JsonPropertyName("gallery_checked_at_utc")] string? GalleryCheckedAtUtc,
    [property: JsonPropertyName("gallery_urls")] List<string> GalleryUrls,
    [property: JsonPropertyName("gallery_images_found")] int GalleryImagesFound,
    [property: JsonPropertyName("gallery_truncated")] bool GalleryTruncated,
    [property: JsonPropertyName("collection_state")] string CollectionState);

sealed record Options(
    string Snapshot,
    string? PreviousResults,
    string Output,
    int BatchSize,
    int MaxImagesPerMap,
    int RefreshAfterDays,
    int DelayMilliseconds,
    int MaxItems) {

  internal static Options Parse(string[] args) {
    var values = new Dictionary<string, string>();
    for (var index = 0; index < args.Length; index += 2) {
      if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal)) {
        throw new ArgumentException(
            "Usage: SteamWorkshopGalleryIndexer --snapshot <path> --output <path> "
            + "[--previous-results <path>] [--batch-size <1-100>] [--max-images-per-map <1-32>] "
            + "[--refresh-after-days <days>] [--delay-milliseconds <milliseconds>] [--max-items <count>]");
      }
      values[args[index]] = args[index + 1];
    }

    var options = new Options(
        Required(values, "--snapshot"),
        values.GetValueOrDefault("--previous-results"),
        Required(values, "--output"),
        ParseInt(values, "--batch-size", 100),
        ParseInt(values, "--max-images-per-map", 8),
        ParseInt(values, "--refresh-after-days", 90),
        ParseInt(values, "--delay-milliseconds", 250),
        ParseInt(values, "--max-items", 0));
    if (options.BatchSize is < 1 or > 100) {
      throw new ArgumentOutOfRangeException(nameof(BatchSize), "--batch-size must be between 1 and 100.");
    }
    if (options.MaxImagesPerMap is < 1 or > 32) {
      throw new ArgumentOutOfRangeException(
          nameof(MaxImagesPerMap), "--max-images-per-map must be between 1 and 32.");
    }
    if (options.RefreshAfterDays < 1 || options.DelayMilliseconds < 0 || options.MaxItems < 0) {
      throw new ArgumentOutOfRangeException(nameof(args), "Numeric options cannot be negative or zero where required.");
    }
    return options;
  }

  static string Required(IReadOnlyDictionary<string, string> values, string name) {
    return values.GetValueOrDefault(name)
        ?? throw new ArgumentException($"Missing required option {name}.");
  }

  static int ParseInt(IReadOnlyDictionary<string, string> values, string name, int defaultValue) {
    return values.TryGetValue(name, out var value)
        ? int.Parse(value)
        : defaultValue;
  }
}

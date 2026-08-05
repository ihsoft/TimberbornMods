using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steamworks;

const uint AppId = 1062090;
const uint MaximumPages = 200;
const int MaximumTransientRetries = 2;
var options = Options.Parse(args);
if (options is null) {
  return 2;
}

Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());
try {
  if (!Packsize.Test() || !DllCheck.Test()) {
    throw new InvalidOperationException("Steamworks.NET native library validation failed.");
  }
  var initResult = GameServer.InitEx(
      0, 0, 0, EServerMode.eServerModeNoAuthentication, "timberborn-workshop-indexer", out var initError);
  if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK) {
    throw new InvalidOperationException($"Steam game-server initialization failed: {initResult}: {initError}");
  }

  try {
    ConnectAnonymously(options.RequestTimeout);
    var records = CollectSnapshot(options.RequestTimeout);
    var outputPath = Path.GetFullPath(options.OutputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    WriteJsonLines(outputPath, records.Items);
    WriteSummary(outputPath, records.Items, records.TotalMatching, records.PagesProcessed);
    Console.WriteLine($"Wrote {records.Items.Count} Workshop items to {outputPath}");
  } finally {
    SteamGameServer.LogOff();
    GameServer.Shutdown();
  }
  return 0;
}
catch (Exception exception) {
  Console.Error.WriteLine(exception.Message);
  return 4;
}

static SnapshotResult CollectSnapshot(TimeSpan timeout) {
  var rawRecords = new List<RawWorkshopRecord>();
  var seenIds = new HashSet<string>();
  uint? expectedTotal = null;
  uint pagesProcessed = 0;
  for (uint page = 1; page <= MaximumPages; page++) {
    var result = QueryPageWithRetry(page, timeout);
    expectedTotal ??= result.TotalMatching;
    if (result.TotalMatching != expectedTotal) {
      throw new InvalidDataException(
          $"Workshop total changed while collecting the snapshot: {expectedTotal} to {result.TotalMatching} on page {page}.");
    }
    foreach (var item in result.Items) {
      if (!seenIds.Add(item.PublishedFileId)) {
        throw new InvalidDataException($"Workshop item {item.PublishedFileId} appeared more than once.");
      }
      rawRecords.Add(item);
    }
    pagesProcessed++;
    Console.WriteLine($"Page {page}: {result.Items.Count} items; collected {rawRecords.Count} / {expectedTotal}.");
    if (rawRecords.Count >= expectedTotal) {
      break;
    }
    if (result.Items.Count == 0) {
      throw new InvalidDataException($"Workshop page {page} was empty before the expected total was collected.");
    }
  }
  if (expectedTotal is null || rawRecords.Count != expectedTotal) {
    throw new InvalidDataException($"Incomplete Workshop snapshot: collected {rawRecords.Count} / {expectedTotal ?? 0} items.");
  }
  return new SnapshotResult(
      expectedTotal.Value, pagesProcessed,
      rawRecords.Select(Classify).OrderByDescending(record => record.UpdatedAtUtc).ToList());
}

static PageResult QueryPageWithRetry(uint page, TimeSpan timeout) {
  for (var retry = 0; ; retry++) {
    var result = QueryPage(page, timeout);
    if (!result.IoFailure && result.Result == EResult.k_EResultOK) {
      return new PageResult(result.TotalMatching, result.Items);
    }
    var diagnostic = FormatFailure(page, result);
    if (!IsTransient(result.Result) || retry >= MaximumTransientRetries) {
      throw new InvalidOperationException($"Workshop query failed: {diagnostic}");
    }
    Console.Error.WriteLine(
        $"Workshop query returned a transient failure; retrying in 10 seconds ({retry + 1} / {MaximumTransientRetries}): {diagnostic}");
    Thread.Sleep(TimeSpan.FromSeconds(10));
  }
}

static QueryAttempt QueryPage(uint page, TimeSpan timeout) {
  var app = new AppId_t(AppId);
  var query = SteamGameServerUGC.CreateQueryAllUGCRequest(
      EUGCQuery.k_EUGCQuery_RankedByLastUpdatedDate,
      EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items_ReadyToUse,
      app, app, page);
  if (query == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException($"Could not create anonymous Workshop query for page {page}.");
  }
  try {
    if (!SteamGameServerUGC.SetReturnLongDescription(query, true)
        || !SteamGameServerUGC.SetLanguage(query, "english")) {
      throw new InvalidOperationException($"Could not configure anonymous Workshop query for page {page}.");
    }
    var completed = false;
    var ioFailure = false;
    SteamUGCQueryCompleted_t response = default;
    using var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
    var apiCall = SteamGameServerUGC.SendQueryUGCRequest(query);
    callResult.Set(apiCall, (result, failed) => {
      response = result;
      ioFailure = failed;
      completed = true;
    });
    WaitForCallback(() => completed, $"Workshop page {page}", timeout);
    var apiFailure = ioFailure
        ? SteamGameServerUtils.GetAPICallFailureReason(apiCall)
        : ESteamAPICallFailure.k_ESteamAPICallFailureNone;
    var items = new List<RawWorkshopRecord>();
    if (!ioFailure && response.m_eResult == EResult.k_EResultOK) {
      for (uint index = 0; index < response.m_unNumResultsReturned; index++) {
        if (!SteamGameServerUGC.GetQueryUGCResult(query, index, out var details)) {
          throw new InvalidDataException($"Workshop result {index} on page {page} was unavailable.");
        }
        if (details.m_eResult != EResult.k_EResultOK) {
          throw new InvalidDataException(
              $"Workshop item {details.m_nPublishedFileId.m_PublishedFileId} on page {page} returned {details.m_eResult}.");
        }
        if (!SteamGameServerUGC.GetQueryUGCPreviewURL(query, index, out var previewUrl, 4096)) {
          previewUrl = string.Empty;
        }
        items.Add(new RawWorkshopRecord(
            details.m_nPublishedFileId.m_PublishedFileId.ToString(), details.m_rgchTitle, details.m_rgchDescription,
            details.m_ulSteamIDOwner.ToString(), details.m_rtimeCreated, details.m_rtimeUpdated, previewUrl,
            details.m_rgchTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            details.m_unVotesUp, details.m_unVotesDown, details.m_flScore));
      }
    }
    return new QueryAttempt(
        response.m_eResult, ioFailure, apiFailure, SteamGameServer.BLoggedOn(), response.m_bCachedData,
        response.m_unNumResultsReturned, response.m_unTotalMatchingResults, items);
  } finally {
    SteamGameServerUGC.ReleaseQueryUGCRequest(query);
  }
}

static string FormatFailure(uint page, QueryAttempt attempt) {
  return $"page={page}, result={attempt.Result}, io_failure={attempt.IoFailure}, "
      + $"api_failure={attempt.ApiFailure}, logged_on={attempt.LoggedOn}, cached={attempt.CachedData}, "
      + $"returned={attempt.Returned}, total={attempt.TotalMatching}";
}

static bool IsTransient(EResult result) {
  return result is EResult.k_EResultBusy or EResult.k_EResultNoConnection;
}

static void ConnectAnonymously(TimeSpan timeout) {
  var connected = false;
  var failure = EResult.k_EResultNone;
  using var connectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(_ => connected = true);
  using var failedCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(result => failure = result.m_eResult);
  SteamGameServer.LogOnAnonymous();
  WaitForCallback(() => connected || failure != EResult.k_EResultNone, "anonymous server login", timeout);
  if (!connected) {
    throw new InvalidOperationException($"Anonymous server login failed: {failure}.");
  }
}

static void WaitForCallback(Func<bool> completed, string operation, TimeSpan timeout) {
  var deadline = DateTime.UtcNow.Add(timeout);
  while (!completed() && DateTime.UtcNow < deadline) {
    GameServer.RunCallbacks();
    Thread.Sleep(100);
  }
  if (!completed()) {
    throw new TimeoutException(
        $"Timed out waiting for {operation}; logged_on={SteamGameServer.BLoggedOn()} after {timeout.TotalSeconds:0} seconds.");
  }
}

static WorkshopRecord Classify(RawWorkshopRecord item) {
  var classification = WorkshopCategoryClassifier.Classify(item.Title, item.Description, item.Tags);
  return new WorkshopRecord(
      item.PublishedFileId, item.Title, item.Description, StripSteamMarkup(item.Description), item.CreatorSteamId,
      DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt).UtcDateTime,
      DateTimeOffset.FromUnixTimeSeconds(item.UpdatedAt).UtcDateTime, item.PreviewUrl, item.Tags,
      item.VotesUp, item.VotesDown, item.Score, classification.PrimaryCategory, classification.Matches);
}

static string StripSteamMarkup(string value) {
  return Regex.Replace(Regex.Replace(value, @"\[/?[^\]]+\]", " "), @"\s+", " ").Trim();
}

static void WriteJsonLines(string outputPath, IEnumerable<WorkshopRecord> records) {
  using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
  foreach (var record in records) {
    writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions()));
  }
}

static JsonSerializerOptions JsonOptions() {
  return new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
}

static void WriteSummary(
    string outputPath, IReadOnlyList<WorkshopRecord> records, uint totalMatching, uint pagesProcessed) {
  var summary = new {
    generated_at_utc = DateTime.UtcNow,
    app_id = AppId,
    source = "anonymous-steam-ugc",
    collected_items = records.Count,
    steam_total_matching = totalMatching,
    pages_processed = pagesProcessed,
    primary_category_counts = records.GroupBy(record => record.PrimaryCategory)
        .OrderByDescending(group => group.Count()).ToDictionary(group => group.Key, group => group.Count()),
  };
  File.WriteAllText(
      Path.ChangeExtension(outputPath, ".summary.json"),
      JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
}

sealed record Options(string OutputPath, TimeSpan RequestTimeout) {
  public static Options? Parse(string[] args) {
    var output = Path.Combine(".tools", "workshop-index", "timberborn-workshop-bootstrap.jsonl");
    var requestTimeout = TimeSpan.FromSeconds(120);
    for (var index = 0; index < args.Length; index++) {
      switch (args[index]) {
        case "--output" when index + 1 < args.Length:
          output = args[++index];
          break;
        case "--request-timeout-seconds" when index + 1 < args.Length
            && int.TryParse(args[++index], out var seconds) && seconds > 0:
          requestTimeout = TimeSpan.FromSeconds(seconds);
          break;
        case "--help":
          PrintUsage();
          return null;
        default:
          Console.Error.WriteLine($"Unknown or incomplete argument: {args[index]}");
          PrintUsage();
          return null;
      }
    }
    return new Options(output, requestTimeout);
  }

  static void PrintUsage() {
    Console.WriteLine("SteamWorkshopIndexer [--output <jsonl>] [--request-timeout-seconds <seconds>]");
  }
}

sealed record QueryAttempt(
    EResult Result, bool IoFailure, ESteamAPICallFailure ApiFailure, bool LoggedOn, bool CachedData,
    uint Returned, uint TotalMatching, List<RawWorkshopRecord> Items);
sealed record PageResult(uint TotalMatching, List<RawWorkshopRecord> Items);
sealed record SnapshotResult(uint TotalMatching, uint PagesProcessed, List<WorkshopRecord> Items);
sealed record RawWorkshopRecord(
    string PublishedFileId, string Title, string Description, string CreatorSteamId, uint CreatedAt, uint UpdatedAt,
    string PreviewUrl, List<string> Tags, uint VotesUp, uint VotesDown, float Score);
sealed record WorkshopRecord(
    string PublishedFileId, string Title, string DescriptionRaw, string DescriptionPlain, string CreatorSteamId,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string PreviewUrl, List<string> Tags,
    uint VotesUp, uint VotesDown, float Score, string PrimaryCategory, List<CategoryMatch> Categories);

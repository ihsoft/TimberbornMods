// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IgorZ.MapBrowser.WorkshopIndexing.Classifiers;
using Steamworks;

namespace IgorZ.MapBrowser.WorkshopIndexing;

sealed class WorkshopIndexer {
  const uint AppId = 1062090;
  const uint MaximumPages = 200;
  const int MaximumTransientRetries = 2;

  sealed record QueryAttempt(
      EResult Result, bool IoFailure, ESteamAPICallFailure ApiFailure, bool LoggedOn, bool CachedData,
      uint Returned, uint TotalMatching, uint SkippedUnavailable, List<RawWorkshopRecord> Items);

  sealed record PageResult(
      uint TotalMatching, uint ResultsProcessed, uint SkippedUnavailable, bool CachedData,
      List<RawWorkshopRecord> Items);

  sealed record SnapshotResult(
      uint TotalMatching, uint PagesProcessed, uint SkippedUnavailable, List<WorkshopRecord> Items);

  sealed record RawWorkshopRecord(
      string PublishedFileId, string Title, string Description, string CreatorSteamId, uint CreatedAt, uint UpdatedAt,
      long PayloadSizeBytes, string PreviewUrl, List<string> Tags, uint VotesUp, uint VotesDown, float Score);

  sealed record WorkshopRecord(
      string PublishedFileId, string Title, string DescriptionRaw, string DescriptionPlain, string CreatorSteamId,
      DateTime CreatedAtUtc, DateTime UpdatedAtUtc, long PayloadSizeBytes, string PreviewUrl, List<string> Tags,
      uint VotesUp, uint VotesDown, float Score, string PrimaryCategory, List<WorkshopCategoryMatch> Categories);

  readonly WorkshopCategoryClassifier _categoryClassifier = new();

  /// <summary>Collects and writes one complete public Workshop metadata snapshot.</summary>
  public int Run(WorkshopIndexerOptions options) {
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
        WriteSummary(
            outputPath, records.Items, records.TotalMatching, records.PagesProcessed, records.SkippedUnavailable);
        Console.WriteLine($"Wrote {records.Items.Count} Workshop items to {outputPath}");
      } finally {
        SteamGameServer.LogOff();
        GameServer.Shutdown();
      }
      return 0;
    } catch (Exception exception) {
      Console.Error.WriteLine(exception.Message);
      return 4;
    }
  }

  SnapshotResult CollectSnapshot(TimeSpan timeout) {
    var rawRecords = new List<RawWorkshopRecord>();
    var seenIds = new HashSet<string>();
    uint? expectedTotal = null;
    uint pagesProcessed = 0;
    uint resultsProcessed = 0;
    uint skippedUnavailable = 0;
    for (uint page = 1; page <= MaximumPages; page++) {
      var result = QueryPageWithRetry(page, timeout);
      expectedTotal ??= result.TotalMatching;
      if (result.TotalMatching != expectedTotal) {
        Console.Error.WriteLine(
            $"Workshop total changed while collecting the snapshot: {expectedTotal} to "
            + $"{result.TotalMatching} on page {page}; cached={result.CachedData}. Continuing with the new total.");
        expectedTotal = result.TotalMatching;
      }
      // Steam's total counts result positions, including unavailable items that we intentionally do not publish.
      resultsProcessed = checked(resultsProcessed + result.ResultsProcessed);
      skippedUnavailable = checked(skippedUnavailable + result.SkippedUnavailable);
      foreach (var item in result.Items) {
        if (!seenIds.Add(item.PublishedFileId)) {
          Console.Error.WriteLine(
              $"Skipping duplicate Workshop item {item.PublishedFileId} on page {page}; "
              + "a later snapshot will converge after the live catalog stabilizes.");
          continue;
        }
        rawRecords.Add(item);
      }
      pagesProcessed++;
      Console.WriteLine(
          $"Page {page}: {result.Items.Count} items, {result.SkippedUnavailable} unavailable; "
          + $"processed {resultsProcessed} / {expectedTotal}; cached={result.CachedData}.");
      if (resultsProcessed >= expectedTotal) {
        break;
      }
      if (result.ResultsProcessed == 0) {
        throw new InvalidDataException($"Workshop page {page} was empty before the expected total was collected.");
      }
    }
    if (expectedTotal is null || resultsProcessed < expectedTotal) {
      throw new InvalidDataException(
          $"Incomplete Workshop snapshot: processed {resultsProcessed} / {expectedTotal ?? 0} results.");
    }
    if (resultsProcessed > expectedTotal) {
      Console.Error.WriteLine(
          $"Workshop snapshot processed {resultsProcessed} positions for the latest reported total "
          + $"of {expectedTotal}; continuing with the collected results.");
    }
    return new SnapshotResult(
        expectedTotal.Value, pagesProcessed, skippedUnavailable,
        rawRecords.Select(Classify).OrderByDescending(record => record.UpdatedAtUtc).ToList());
  }

  PageResult QueryPageWithRetry(uint page, TimeSpan timeout) {
    for (var retry = 0; ; retry++) {
      var result = QueryPage(page, timeout);
      if (!result.IoFailure && result.Result == EResult.k_EResultOK) {
        return new PageResult(
            result.TotalMatching, result.Returned, result.SkippedUnavailable, result.CachedData, result.Items);
      }
      var diagnostic = FormatFailure(page, result);
      if (!IsTransient(result.Result) || retry >= MaximumTransientRetries) {
        throw new InvalidOperationException($"Workshop query failed: {diagnostic}");
      }
      Console.Error.WriteLine(
          $"Workshop query returned a transient failure; retrying in 10 seconds "
          + $"({retry + 1} / {MaximumTransientRetries}): {diagnostic}");
      Thread.Sleep(TimeSpan.FromSeconds(10));
    }
  }

  QueryAttempt QueryPage(uint page, TimeSpan timeout) {
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
      uint skippedUnavailable = 0;
      if (!ioFailure && response.m_eResult == EResult.k_EResultOK) {
        for (uint index = 0; index < response.m_unNumResultsReturned; index++) {
          if (!SteamGameServerUGC.GetQueryUGCResult(query, index, out var details)) {
            throw new InvalidDataException($"Workshop result {index} on page {page} was unavailable.");
          }
          if (details.m_eResult != EResult.k_EResultOK) {
            if (details.m_eResult == EResult.k_EResultFileNotFound) {
              skippedUnavailable++;
              Console.Error.WriteLine(
                  $"Skipping unavailable Workshop item {details.m_nPublishedFileId.m_PublishedFileId} "
                  + $"on page {page}: {details.m_eResult}.");
              continue;
            }
            throw new InvalidDataException(
                $"Workshop item {details.m_nPublishedFileId.m_PublishedFileId} on page {page} "
                + $"returned {details.m_eResult}.");
          }
          if (!SteamGameServerUGC.GetQueryUGCPreviewURL(query, index, out var previewUrl, 4096)) {
            previewUrl = string.Empty;
          }
          items.Add(new RawWorkshopRecord(
              details.m_nPublishedFileId.m_PublishedFileId.ToString(), details.m_rgchTitle, details.m_rgchDescription,
              details.m_ulSteamIDOwner.ToString(), details.m_rtimeCreated, details.m_rtimeUpdated,
              details.m_nFileSize, previewUrl,
              details.m_rgchTags.Split(
                  ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
              details.m_unVotesUp, details.m_unVotesDown, details.m_flScore));
        }
      }
      return new QueryAttempt(
          response.m_eResult, ioFailure, apiFailure, SteamGameServer.BLoggedOn(), response.m_bCachedData,
          response.m_unNumResultsReturned, response.m_unTotalMatchingResults, skippedUnavailable, items);
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

  void ConnectAnonymously(TimeSpan timeout) {
    var connected = false;
    var failure = EResult.k_EResultNone;
    using var connectedCallback = Callback<SteamServersConnected_t>.CreateGameServer(_ => connected = true);
    using var failedCallback = Callback<SteamServerConnectFailure_t>.CreateGameServer(
        result => failure = result.m_eResult);
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
          $"Timed out waiting for {operation}; logged_on={SteamGameServer.BLoggedOn()} "
          + $"after {timeout.TotalSeconds:0} seconds.");
    }
  }

  WorkshopRecord Classify(RawWorkshopRecord item) {
    var classification = _categoryClassifier.Classify(item.Title, item.Description, item.Tags);
    return new WorkshopRecord(
        item.PublishedFileId, item.Title, item.Description, StripSteamMarkup(item.Description), item.CreatorSteamId,
        DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt).UtcDateTime,
        DateTimeOffset.FromUnixTimeSeconds(item.UpdatedAt).UtcDateTime, item.PayloadSizeBytes,
        item.PreviewUrl, item.Tags, item.VotesUp, item.VotesDown, item.Score,
        classification.PrimaryCategory, classification.Matches);
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
      string outputPath, IReadOnlyList<WorkshopRecord> records, uint totalMatching, uint pagesProcessed,
      uint skippedUnavailable) {
    var summary = new {
        generated_at_utc = DateTime.UtcNow,
        app_id = AppId,
        source = "anonymous-steam-ugc",
        collected_items = records.Count,
        steam_total_matching = totalMatching,
        pages_processed = pagesProcessed,
        skipped_unavailable = skippedUnavailable,
        primary_category_counts = records.GroupBy(record => record.PrimaryCategory)
            .OrderByDescending(group => group.Count()).ToDictionary(group => group.Key, group => group.Count()),
    };
    File.WriteAllText(
        Path.ChangeExtension(outputPath, ".summary.json"),
        JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
  }
}

using System.Text.Json;
using Steamworks;

namespace IgorZ.TimberbornMods.Tools.SteamTagUpdating;

static class Program {
  public static int Main(string[] args) => new SteamTagUpdater().Run(args);
}

sealed class SteamTagUpdater {
  const uint AppId = 1062090;
  static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(120);

  public int Run(string[] args) {
    if (args is ["--diagnose"]) {
      return Diagnose();
    }
    if (args is ["--query", var publishedFileId]) {
      return Query(ulong.Parse(publishedFileId));
    }
    if (args.Length < 2) {
      Console.Error.WriteLine("Usage: SteamTagUpdater --diagnose");
      Console.Error.WriteLine("Usage: SteamTagUpdater <publishedFileId> <tag> [<tag>...]");
      return 2;
    }

    return UpdateTags(ulong.Parse(args[0]), args.Skip(1).ToArray());
  }

  int UpdateTags(ulong publishedFileId, string[] tags) {
    if (!InitializeSteam()) {
      return 3;
    }

    try {
      Console.WriteLine($"SteamUtils.GetAppID: {SteamUtils.GetAppID().m_AppId}");
      Console.WriteLine($"SteamUser.BLoggedOn: {SteamUser.BLoggedOn()}");
      Console.WriteLine($"SteamUser.GetSteamID: {SteamUser.GetSteamID().m_SteamID}");
      Console.WriteLine($"Target tags: {string.Join(", ", tags)}");

      var updateHandle = SteamUGC.StartItemUpdate(new AppId_t(AppId), new PublishedFileId_t(publishedFileId));
      Console.WriteLine($"Update handle: {updateHandle.m_UGCUpdateHandle}");
      if (!SteamUGC.SetItemTags(updateHandle, tags)) {
        Console.Error.WriteLine("SetItemTags returned false.");
        return 4;
      }

      var completed = false;
      var ioFailureResult = false;
      var steamResult = EResult.k_EResultNone;
      var call = SteamUGC.SubmitItemUpdate(updateHandle, "Update tags");
      using var callResult = CallResult<SubmitItemUpdateResult_t>.Create();
      callResult.Set(call, (result, ioFailure) => {
        completed = true;
        ioFailureResult = ioFailure;
        steamResult = result.m_eResult;
        Console.WriteLine(
            $"SubmitItemUpdate callback: result={steamResult}, ioFailure={ioFailureResult}, "
            + $"needsAgreement={result.m_bUserNeedsToAcceptWorkshopLegalAgreement}");
      });
      WaitForCallback(() => completed);
      if (!completed) {
        Console.Error.WriteLine("Timed out waiting for SubmitItemUpdate callback.");
        return 5;
      }
      return !ioFailureResult && steamResult == EResult.k_EResultOK ? 0 : 6;
    } finally {
      SteamAPI.Shutdown();
    }
  }

  int Diagnose() {
    if (!InitializeSteam()) {
      return 3;
    }
    try {
      Console.WriteLine($"CurrentDirectory: {Environment.CurrentDirectory}");
      Console.WriteLine($"BaseDirectory: {AppContext.BaseDirectory}");
      Console.WriteLine($"SteamUtils.GetAppID: {SteamUtils.GetAppID().m_AppId}");
      Console.WriteLine($"SteamUser.BLoggedOn: {SteamUser.BLoggedOn()}");
      Console.WriteLine($"SteamUser.GetSteamID: {SteamUser.GetSteamID().m_SteamID}");
      return 0;
    } finally {
      SteamAPI.Shutdown();
    }
  }

  int Query(ulong publishedFileId) {
    if (!InitializeSteam()) {
      return 3;
    }
    try {
      var ids = new[] { new PublishedFileId_t(publishedFileId) };
      var query = SteamUGC.CreateQueryUGCDetailsRequest(ids, 1);
      if (query == UGCQueryHandle_t.Invalid) {
        Console.Error.WriteLine("Could not create Workshop details query.");
        return 4;
      }
      try {
        var completed = false;
        var ioFailureResult = false;
        SteamUGCQueryCompleted_t response = default;
        using var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
        callResult.Set(SteamUGC.SendQueryUGCRequest(query), (result, ioFailure) => {
          response = result;
          ioFailureResult = ioFailure;
          completed = true;
        });
        WaitForCallback(() => completed);
        if (!completed || ioFailureResult || response.m_eResult != EResult.k_EResultOK
            || response.m_unNumResultsReturned != 1
            || !SteamUGC.GetQueryUGCResult(query, 0, out var details)) {
          Console.Error.WriteLine($"Workshop details query failed: {response.m_eResult}.");
          return 5;
        }

        var tags = new List<string>();
        for (uint index = 0; index < 100; index++) {
          if (!SteamUGC.GetQueryUGCTag(query, 0, index, out var tag, 1024)) {
            break;
          }
          tags.Add(tag);
        }
        Console.WriteLine($"LIVE_TAGS_JSON={JsonSerializer.Serialize(tags)}");
        Console.WriteLine($"LIVE_ITEM_JSON={JsonSerializer.Serialize(new {
            Visibility = details.m_eVisibility.ToString(),
            FileSize = details.m_nFileSize,
            Updated = details.m_rtimeUpdated,
        })}");
        return 0;
      } finally {
        SteamUGC.ReleaseQueryUGCRequest(query);
      }
    } finally {
      SteamAPI.Shutdown();
    }
  }

  static bool InitializeSteam() {
    Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
    Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());
    Console.WriteLine($"Packsize.Test: {Packsize.Test()}");
    Console.WriteLine($"DllCheck.Test: {DllCheck.Test()}");
    Console.WriteLine("SteamAPI.Init...");
    if (!SteamAPI.Init()) {
      Console.Error.WriteLine("SteamAPI.Init failed. Ensure Steam is running and logged in.");
      return false;
    }
    if (!SteamUser.BLoggedOn()) {
      Console.Error.WriteLine("Steam is running, but the current user is not logged on.");
      SteamAPI.Shutdown();
      return false;
    }
    return true;
  }

  static void WaitForCallback(Func<bool> completed) {
    var deadline = DateTime.UtcNow.Add(CallbackTimeout);
    while (!completed() && DateTime.UtcNow < deadline) {
      SteamAPI.RunCallbacks();
      Thread.Sleep(100);
    }
  }
}

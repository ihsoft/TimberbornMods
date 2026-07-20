using Steamworks;

if (args.Length == 1 && args[0] == "--diagnose") {
  return Diagnose();
}

if (args.Length == 2 && args[0] == "--query") {
  return Query(ulong.Parse(args[1]));
}

if (args.Length < 2) {
  Console.Error.WriteLine("Usage: SteamTagUpdater --diagnose");
  Console.Error.WriteLine("Usage: SteamTagUpdater <publishedFileId> <tag> [<tag>...]");
  return 2;
}

var publishedFileId = ulong.Parse(args[0]);
var tags = args.Skip(1).ToArray();

if (!InitializeSteam()) {
  return 3;
}

try {
  Console.WriteLine($"SteamUtils.GetAppID: {SteamUtils.GetAppID().m_AppId}");
  Console.WriteLine($"SteamUser.BLoggedOn: {SteamUser.BLoggedOn()}");
  Console.WriteLine($"SteamUser.GetSteamID: {SteamUser.GetSteamID().m_SteamID}");
  Console.WriteLine($"Target tags: {string.Join(", ", tags)}");

  var updateHandle = SteamUGC.StartItemUpdate(new AppId_t(1062090), new PublishedFileId_t(publishedFileId));
  Console.WriteLine($"Update handle: {updateHandle.m_UGCUpdateHandle}");

  if (!SteamUGC.SetItemTags(updateHandle, tags)) {
    Console.Error.WriteLine("SetItemTags returned false.");
    return 4;
  }

  var completed = false;
  var ioFailureResult = false;
  var steamResult = EResult.k_EResultNone;
  var call = SteamUGC.SubmitItemUpdate(updateHandle, "Update tags");
  var callResult = CallResult<SubmitItemUpdateResult_t>.Create();
  callResult.Set(call, (result, ioFailure) => {
    completed = true;
    ioFailureResult = ioFailure;
    steamResult = result.m_eResult;
    Console.WriteLine(
        $"SubmitItemUpdate callback: result={steamResult}, ioFailure={ioFailureResult}, " +
        $"needsAgreement={result.m_bUserNeedsToAcceptWorkshopLegalAgreement}");
  });

  var deadline = DateTime.UtcNow.AddSeconds(120);
  while (!completed && DateTime.UtcNow < deadline) {
    SteamAPI.RunCallbacks();
    Thread.Sleep(100);
  }

  if (!completed) {
    Console.Error.WriteLine("Timed out waiting for SubmitItemUpdate callback.");
    return 5;
  }

  return !ioFailureResult && steamResult == EResult.k_EResultOK ? 0 : 6;
}
finally {
  SteamAPI.Shutdown();
}

static int Diagnose() {
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
  }
  finally {
    SteamAPI.Shutdown();
  }
}

static int Query(ulong publishedFileId) {
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
      var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
      callResult.Set(SteamUGC.SendQueryUGCRequest(query), (result, ioFailure) => {
        response = result;
        ioFailureResult = ioFailure;
        completed = true;
      });
      var deadline = DateTime.UtcNow.AddSeconds(120);
      while (!completed && DateTime.UtcNow < deadline) {
        SteamAPI.RunCallbacks();
        Thread.Sleep(100);
      }
      if (!completed || ioFailureResult || response.m_eResult != EResult.k_EResultOK
          || response.m_unNumResultsReturned != 1
          || !SteamUGC.GetQueryUGCResult(query, 0, out var details)) {
        Console.Error.WriteLine($"Workshop details query failed: {response.m_eResult}.");
        return 5;
      }

      var tags = new List<string>();
      for (uint i = 0; i < 100; ++i) {
        if (!SteamUGC.GetQueryUGCTag(query, 0, i, out var tag, 1024)) {
          break;
        }
        tags.Add(tag);
      }
      Console.WriteLine($"LIVE_TAGS_JSON={System.Text.Json.JsonSerializer.Serialize(tags)}");
      Console.WriteLine($"LIVE_ITEM_JSON={System.Text.Json.JsonSerializer.Serialize(new {
          Visibility = details.m_eVisibility.ToString(),
          FileSize = details.m_nFileSize,
          Updated = details.m_rtimeUpdated,
      })}");
      return 0;
    }
    finally {
      SteamUGC.ReleaseQueryUGCRequest(query);
    }
  }
  finally {
    SteamAPI.Shutdown();
  }
}

static bool InitializeSteam() {
  const uint appId = 1062090;
  Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
  Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
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

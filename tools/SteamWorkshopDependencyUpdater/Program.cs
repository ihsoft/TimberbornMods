using Steamworks;

if (args.Length != 3 || args[0] is not ("--dry-run" or "--publish")
    || !ulong.TryParse(args[1], out var parentId) || !ulong.TryParse(args[2], out var childId)) {
  Console.Error.WriteLine("Usage: SteamWorkshopDependencyUpdater --dry-run|--publish <parent-id> <child-id>");
  return 2;
}

const uint appId = 1062090;
var publish = args[0] == "--publish";
Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
if (!Packsize.Test() || !DllCheck.Test() || !SteamAPI.Init()) {
  Console.Error.WriteLine("Steam initialization failed. Ensure Steam is running and logged in.");
  return 3;
}

try {
  if (!SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId != appId) {
    throw new InvalidOperationException("Steam account or application scope is invalid.");
  }

  var parent = QueryItem(parentId);
  var child = QueryItem(childId);
  if (parent.ConsumerAppId != appId || child.ConsumerAppId != appId) {
    throw new InvalidOperationException("Both Workshop items must belong to Timberborn.");
  }

  Console.WriteLine("Steam Workshop dependency plan");
  Console.WriteLine($"  Account: {SteamUser.GetSteamID().m_SteamID}");
  Console.WriteLine($"  Parent: {parent.Title} ({parentId})");
  Console.WriteLine($"  Dependency: {child.Title} ({childId})");
  Console.WriteLine($"  Already configured: {parent.Children.Contains(childId)}");
  if (!publish) {
    Console.WriteLine("Dry run only. No dependency was changed.");
    return 0;
  }
  if (!parent.Children.Contains(childId)) {
    AddDependency(parentId, childId);
  }

  var verified = QueryItem(parentId);
  if (!verified.Children.Contains(childId)) {
    throw new InvalidOperationException("Steam accepted the request, but live dependency verification failed.");
  }
  Console.WriteLine("Steam Workshop dependency is configured and verified.");
  return 0;
}
finally {
  SteamAPI.Shutdown();
}

static ItemDetails QueryItem(ulong publishedFileId) {
  var ids = new[] { new PublishedFileId_t(publishedFileId) };
  var query = SteamUGC.CreateQueryUGCDetailsRequest(ids, 1);
  if (query == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException("Could not create Workshop details query.");
  }
  try {
    if (!SteamUGC.SetReturnChildren(query, true)) {
      throw new InvalidOperationException("Could not request Workshop dependency details.");
    }
    var completed = false;
    SteamUGCQueryCompleted_t response = default;
    var ioFailureResult = false;
    var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
    callResult.Set(SteamUGC.SendQueryUGCRequest(query), (result, ioFailure) => {
      response = result;
      ioFailureResult = ioFailure;
      completed = true;
    });
    WaitForCallback(() => completed, "Workshop details query");
    if (ioFailureResult || response.m_eResult != EResult.k_EResultOK || response.m_unNumResultsReturned != 1
        || !SteamUGC.GetQueryUGCResult(query, 0, out var details)) {
      throw new InvalidOperationException($"Workshop query failed: {response.m_eResult}.");
    }

    var children = new PublishedFileId_t[details.m_unNumChildren];
    if (children.Length > 0 && !SteamUGC.GetQueryUGCChildren(query, 0, children, (uint)children.Length)) {
      throw new InvalidOperationException("Could not read Workshop dependencies.");
    }
    return new ItemDetails(
        details.m_nConsumerAppID.m_AppId, details.m_rgchTitle,
        children.Select(child => child.m_PublishedFileId).ToHashSet());
  }
  finally {
    SteamUGC.ReleaseQueryUGCRequest(query);
  }
}

static void AddDependency(ulong parentId, ulong childId) {
  var completed = false;
  AddUGCDependencyResult_t response = default;
  var ioFailureResult = false;
  var callResult = CallResult<AddUGCDependencyResult_t>.Create();
  callResult.Set(
      SteamUGC.AddDependency(new PublishedFileId_t(parentId), new PublishedFileId_t(childId)),
      (result, ioFailure) => {
        response = result;
        ioFailureResult = ioFailure;
        completed = true;
      });
  WaitForCallback(() => completed, "AddDependency");
  if (ioFailureResult || response.m_eResult != EResult.k_EResultOK) {
    throw new InvalidOperationException($"AddDependency failed: {response.m_eResult}.");
  }
}

static void WaitForCallback(Func<bool> completed, string operation) {
  var deadline = DateTime.UtcNow.AddSeconds(120);
  while (!completed() && DateTime.UtcNow < deadline) {
    SteamAPI.RunCallbacks();
    Thread.Sleep(100);
  }
  if (!completed()) {
    throw new TimeoutException($"Timed out waiting for {operation}.");
  }
}

sealed record ItemDetails(uint ConsumerAppId, string Title, HashSet<ulong> Children);

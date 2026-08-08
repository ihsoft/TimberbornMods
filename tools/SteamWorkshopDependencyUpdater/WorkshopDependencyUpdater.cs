using Steamworks;

namespace IgorZ.TimberbornMods.Tools.WorkshopDependencyUpdating;

sealed class WorkshopDependencyUpdater {
  const uint AppId = 1062090;

  sealed record ItemDetails(uint ConsumerAppId, string Title, HashSet<ulong> Children);

  static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(120);

  /// <summary>
  /// Plans or applies one parent-to-child Workshop dependency and verifies live state after mutation.
  /// </summary>
  public int Run(bool publish, ulong parentId, ulong childId) {
    Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
    Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());
    if (!Packsize.Test() || !DllCheck.Test() || !SteamAPI.Init()) {
      Console.Error.WriteLine("Steam initialization failed. Ensure Steam is running and logged in.");
      return 3;
    }

    try {
      if (!SteamUser.BLoggedOn() || SteamUtils.GetAppID().m_AppId != AppId) {
        throw new InvalidOperationException("Steam account or application scope is invalid.");
      }

      var parent = QueryItem(parentId);
      var child = QueryItem(childId);
      if (parent.ConsumerAppId != AppId || child.ConsumerAppId != AppId) {
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

      // A successful Steam callback is not enough: verify that the relationship is visible in live metadata.
      var verified = QueryItem(parentId);
      if (!verified.Children.Contains(childId)) {
        throw new InvalidOperationException("Steam accepted the request, but live dependency verification failed.");
      }
      Console.WriteLine("Steam Workshop dependency is configured and verified.");
      return 0;
    } finally {
      SteamAPI.Shutdown();
    }
  }

  ItemDetails QueryItem(ulong publishedFileId) {
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
      using var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
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
      if (children.Length > 0 && !SteamUGC.GetQueryUGCChildren(query, 0, children, (uint) children.Length)) {
        throw new InvalidOperationException("Could not read Workshop dependencies.");
      }
      return new ItemDetails(
          details.m_nConsumerAppID.m_AppId, details.m_rgchTitle,
          children.Select(child => child.m_PublishedFileId).ToHashSet());
    } finally {
      SteamUGC.ReleaseQueryUGCRequest(query);
    }
  }

  void AddDependency(ulong parentId, ulong childId) {
    var completed = false;
    AddUGCDependencyResult_t response = default;
    var ioFailureResult = false;
    using var callResult = CallResult<AddUGCDependencyResult_t>.Create();
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
    var deadline = DateTime.UtcNow.Add(CallbackTimeout);
    while (!completed() && DateTime.UtcNow < deadline) {
      SteamAPI.RunCallbacks();
      Thread.Sleep(100);
    }
    if (!completed()) {
      throw new TimeoutException($"Timed out waiting for {operation}.");
    }
  }
}

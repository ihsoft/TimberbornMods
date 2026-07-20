using System.Net;
using Steamworks;

const uint appId = 1062090;
var ids = args.Length == 0
    ? new ulong[] { 3535425318, 3532441249, 3383139185 }
    : args.Select(ulong.Parse).ToArray();

Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
if (!Packsize.Test() || !DllCheck.Test()) {
  Console.Error.WriteLine("Steamworks.NET native library validation failed.");
  return 2;
}

var initialized = GameServer.Init(
    0, 0, 0, EServerMode.eServerModeNoAuthentication, "anonymous-ugc-probe");
if (!initialized) {
  Console.Error.WriteLine("Steam game-server initialization failed.");
  return 3;
}

try {
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

  Console.WriteLine("Anonymous Steam game-server session connected.");
  QueryAdditionalPreviews(ids);
  return 0;
}
finally {
  SteamGameServer.LogOff();
  GameServer.Shutdown();
}

static void QueryAdditionalPreviews(IReadOnlyCollection<ulong> publishedFileIds) {
  var ids = publishedFileIds.Select(id => new PublishedFileId_t(id)).ToArray();
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
    Console.WriteLine(
        $"UGC query result: {response.m_eResult}; returned {response.m_unNumResultsReturned}/{ids.Length} items.");
    if (ioFailureResult || response.m_eResult != EResult.k_EResultOK) {
      throw new InvalidOperationException($"Anonymous Workshop query failed: {response.m_eResult}.");
    }

    for (uint itemIndex = 0; itemIndex < response.m_unNumResultsReturned; itemIndex++) {
      if (!SteamGameServerUGC.GetQueryUGCResult(query, itemIndex, out var details)) {
        Console.WriteLine($"Result {itemIndex}: details unavailable.");
        continue;
      }

      var count = SteamGameServerUGC.GetQueryUGCNumAdditionalPreviews(query, itemIndex);
      Console.WriteLine($"{details.m_nPublishedFileId.m_PublishedFileId} | {details.m_rgchTitle} | previews: {count}");
      for (uint previewIndex = 0; previewIndex < count; previewIndex++) {
        if (SteamGameServerUGC.GetQueryUGCAdditionalPreview(
            query, itemIndex, previewIndex, out var url, 4096, out var originalFileName,
            1024, out var previewType)) {
          Console.WriteLine($"  {previewType}: {WebUtility.HtmlDecode(url)} ({originalFileName})");
        } else {
          Console.WriteLine($"  preview {previewIndex}: unavailable");
        }
      }
    }
  }
  finally {
    SteamGameServerUGC.ReleaseQueryUGCRequest(query);
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

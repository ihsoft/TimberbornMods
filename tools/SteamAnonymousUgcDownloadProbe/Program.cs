using Steamworks;

const uint appId = 1062090;
var options = Options.Parse(args);
var publishedFileId = new PublishedFileId_t(options.PublishedFileId);

Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
if (!Packsize.Test() || !DllCheck.Test()) {
  Console.Error.WriteLine("Steamworks.NET native library validation failed.");
  return 2;
}

var initResult = GameServer.InitEx(
    0, 0, 0, EServerMode.eServerModeNoAuthentication, "anonymous-ugc-download-probe", out var initError);
if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK) {
  Console.Error.WriteLine($"Steam game-server initialization failed: {initResult}: {initError}");
  return 3;
}

try {
  ConnectAnonymously(options.Timeout);
  var details = QueryDetails(publishedFileId, options.Timeout);
  Console.WriteLine(
      $"Workshop item {options.PublishedFileId}: {details.m_rgchTitle}; declared size {details.m_nFileSize} bytes.");
  if ((ulong)details.m_nFileSize > options.MaxDownloadBytes) {
    throw new InvalidOperationException(
        $"Declared item size {details.m_nFileSize} exceeds the {options.MaxDownloadBytes}-byte probe limit.");
  }

  DownloadItem(publishedFileId, options.Timeout);
  if (!SteamGameServerUGC.GetItemInstallInfo(
      publishedFileId, out var sizeOnDisk, out var folder, 4096, out var timestamp)) {
    throw new InvalidOperationException("Workshop download completed but GetItemInstallInfo returned false.");
  }
  if (sizeOnDisk > options.MaxDownloadBytes) {
    throw new InvalidOperationException(
        $"Installed item size {sizeOnDisk} exceeds the {options.MaxDownloadBytes}-byte probe limit.");
  }

  Console.WriteLine($"Installed {sizeOnDisk} bytes in the temporary UGC cache; timestamp {timestamp}.");
  var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList();
  foreach (var path in files.Take(100)) {
    Console.WriteLine($"  {Path.GetRelativePath(folder, path)} ({new FileInfo(path).Length} bytes)");
  }
  if (files.Count > 100) {
    Console.WriteLine($"  ... {files.Count - 100} additional files omitted from the diagnostic log.");
  }
  Console.WriteLine($"Anonymous UGC payload probe succeeded with {files.Count} files.");
  return 0;
}
finally {
  SteamGameServer.LogOff();
  GameServer.Shutdown();
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
    throw new InvalidOperationException($"Anonymous server login failed: {connectFailure}.");
  }
  Console.WriteLine("Anonymous Steam game-server session connected.");
}

static SteamUGCDetails_t QueryDetails(PublishedFileId_t publishedFileId, TimeSpan timeout) {
  var query = SteamGameServerUGC.CreateQueryUGCDetailsRequest([publishedFileId], 1);
  if (query == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException("Could not create the anonymous Workshop details query.");
  }

  try {
    var completed = false;
    var ioFailureResult = false;
    SteamUGCQueryCompleted_t response = default;
    using var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
    callResult.Set(SteamGameServerUGC.SendQueryUGCRequest(query), (result, ioFailure) => {
      response = result;
      ioFailureResult = ioFailure;
      completed = true;
    });
    WaitForCallback(() => completed, "anonymous Workshop query", timeout);
    if (ioFailureResult || response.m_eResult != EResult.k_EResultOK || response.m_unNumResultsReturned != 1
        || !SteamGameServerUGC.GetQueryUGCResult(query, 0, out var details)) {
      throw new InvalidOperationException($"Anonymous Workshop query failed: {response.m_eResult}.");
    }
    return details;
  }
  finally {
    SteamGameServerUGC.ReleaseQueryUGCRequest(query);
  }
}

static void DownloadItem(PublishedFileId_t publishedFileId, TimeSpan timeout) {
  var completed = false;
  DownloadItemResult_t response = default;
  using var downloadCallback = Callback<DownloadItemResult_t>.CreateGameServer(result => {
    if (result.m_nPublishedFileId == publishedFileId) {
      response = result;
      completed = true;
    }
  });
  if (!SteamGameServerUGC.DownloadItem(publishedFileId, false)) {
    throw new InvalidOperationException("Steam rejected the anonymous Workshop download request.");
  }

  WaitForCallback(() => completed, "Workshop item download", timeout);
  if (response.m_unAppID.m_AppId != appId || response.m_eResult != EResult.k_EResultOK) {
    throw new InvalidOperationException(
        $"Workshop download failed for app {response.m_unAppID.m_AppId}: {response.m_eResult}.");
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

sealed record Options(ulong PublishedFileId, ulong MaxDownloadBytes, TimeSpan Timeout) {
  internal static Options Parse(string[] args) {
    if (args.Length != 3 || !ulong.TryParse(args[0], out var publishedFileId)
        || !ulong.TryParse(args[1], out var maxDownloadBytes) || !int.TryParse(args[2], out var timeoutSeconds)
        || publishedFileId == 0 || maxDownloadBytes == 0 || timeoutSeconds is < 1 or > 600) {
      throw new ArgumentException(
          "Usage: SteamAnonymousUgcDownloadProbe <published-file-id> <max-download-bytes> <timeout-seconds<=600>");
    }
    return new Options(publishedFileId, maxDownloadBytes, TimeSpan.FromSeconds(timeoutSeconds));
  }
}

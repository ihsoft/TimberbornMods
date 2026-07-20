using System.Text.Json;
using Steamworks;

if (args.Length != 2 || args[0] is not ("--dry-run" or "--create")) {
  Console.Error.WriteLine("Usage: SteamWorkshopItemCreator --dry-run <plan.json>");
  Console.Error.WriteLine("Usage: SteamWorkshopItemCreator --create <plan.json>");
  return 2;
}

var create = args[0] == "--create";
var planPath = Path.GetFullPath(args[1]);
if (!File.Exists(planPath)) {
  Console.Error.WriteLine($"Plan not found: {planPath}");
  return 2;
}

var plan = JsonSerializer.Deserialize<CreationPlan>(File.ReadAllText(planPath), JsonOptions())
    ?? throw new InvalidOperationException("Creation plan is empty.");
ValidatePlan(plan);

var descriptionPath = Path.GetFullPath(plan.DescriptionPath);
var previewPath = Path.GetFullPath(plan.PreviewPath);
var resultPath = Path.GetFullPath(plan.ResultPath);
if (!File.Exists(descriptionPath)) {
  throw new FileNotFoundException("Description source not found.", descriptionPath);
}
if (!File.Exists(previewPath)) {
  throw new FileNotFoundException("Preview image not found.", previewPath);
}
if (File.Exists(resultPath)) {
  throw new InvalidOperationException($"Result already exists; refusing duplicate creation: {resultPath}");
}

var description = File.ReadAllText(descriptionPath);
Console.WriteLine("Steam Workshop identity creation plan");
Console.WriteLine($"  App ID: {plan.AppId}");
Console.WriteLine($"  Title: {plan.Title}");
Console.WriteLine($"  Visibility: {plan.Visibility}");
Console.WriteLine($"  Tags: {string.Join(", ", plan.Tags)}");
Console.WriteLine($"  Description: {descriptionPath}");
Console.WriteLine($"  Preview: {previewPath}");
Console.WriteLine("  Content upload: disabled (this tool has no content-folder input)");
Console.WriteLine($"  Result: {resultPath}");

if (!InitializeSteam(plan.AppId)) {
  return 3;
}

try {
  var steamId = SteamUser.GetSteamID();
  Console.WriteLine($"  Steam user: {steamId.m_SteamID}");
  Console.WriteLine($"  Logged on: {SteamUser.BLoggedOn()}");

  var existingItems = QueryUserItems(steamId.GetAccountID(), plan.AppId);
  var duplicate = existingItems.FirstOrDefault(item =>
      string.Equals(item.Title, plan.Title, StringComparison.OrdinalIgnoreCase));
  if (duplicate is not null) {
    Console.Error.WriteLine(
        $"An item with this exact title already exists: {duplicate.PublishedFileId} ({duplicate.Visibility}).");
    return 4;
  }

  Console.WriteLine("  Existing exact-title item: none");
  if (!create) {
    Console.WriteLine("Dry run only. No Workshop item was created.");
    return 0;
  }

  var created = CreateItem(plan.AppId);
  Console.WriteLine($"Created Workshop identity: {created.PublishedFileId}");
  Console.WriteLine($"Needs legal agreement: {created.NeedsLegalAgreement}");

  try {
    UpdateItemProfile(created.PublishedFileId, plan, description, previewPath);
    var verified = QueryItem(created.PublishedFileId);
    if (verified.ConsumerAppId != plan.AppId ||
        !string.Equals(verified.Title, plan.Title, StringComparison.Ordinal) ||
        verified.Visibility != plan.Visibility) {
      throw new InvalidOperationException(
          $"Live verification mismatch. App={verified.ConsumerAppId}, Title={verified.Title}, " +
          $"Visibility={verified.Visibility}.");
    }

    Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
    var result = new CreationResult(
        created.PublishedFileId,
        plan.AppId,
        steamId.m_SteamID,
        verified.Title,
        verified.Visibility,
        created.NeedsLegalAgreement,
        DateTimeOffset.UtcNow);
    File.WriteAllText(resultPath, JsonSerializer.Serialize(result, JsonOptions()));
    Console.WriteLine($"Verified Workshop identity: {created.PublishedFileId}");
    Console.WriteLine($"CREATED_PUBLISHED_FILE_ID={created.PublishedFileId}");
    return 0;
  }
  catch (Exception e) {
    Console.Error.WriteLine(
        $"Workshop identity {created.PublishedFileId} was created, but profile update or verification failed.");
    Console.Error.WriteLine(e);
    Console.Error.WriteLine($"PARTIAL_PUBLISHED_FILE_ID={created.PublishedFileId}");
    return 5;
  }
}
finally {
  SteamAPI.Shutdown();
}

static JsonSerializerOptions JsonOptions() {
  return new JsonSerializerOptions {
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
  };
}

static void ValidatePlan(CreationPlan plan) {
  if (plan.AppId != 1062090) {
    throw new InvalidOperationException($"Only Timberborn App ID 1062090 is supported, got {plan.AppId}.");
  }
  if (string.IsNullOrWhiteSpace(plan.Title)) {
    throw new InvalidOperationException("Title is required.");
  }
  if (plan.Visibility != "Private") {
    throw new InvalidOperationException("Identity creation supports Private visibility only.");
  }
  if (plan.Tags.Length == 0 || plan.Tags.Any(string.IsNullOrWhiteSpace)) {
    throw new InvalidOperationException("At least one nonempty tag is required.");
  }
  if (string.IsNullOrWhiteSpace(plan.DescriptionPath) ||
      string.IsNullOrWhiteSpace(plan.PreviewPath) ||
      string.IsNullOrWhiteSpace(plan.ResultPath)) {
    throw new InvalidOperationException("DescriptionPath, PreviewPath, and ResultPath are required.");
  }
}

static bool InitializeSteam(uint appId) {
  Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());
  Environment.SetEnvironmentVariable("SteamGameId", appId.ToString());
  if (!Packsize.Test() || !DllCheck.Test()) {
    Console.Error.WriteLine("Steamworks.NET platform checks failed.");
    return false;
  }
  if (!SteamAPI.Init()) {
    Console.Error.WriteLine("SteamAPI.Init failed. Ensure Steam is running and logged in.");
    return false;
  }
  if (!SteamUser.BLoggedOn()) {
    Console.Error.WriteLine("Steam is running, but the current user is not logged on.");
    SteamAPI.Shutdown();
    return false;
  }
  if (SteamUtils.GetAppID().m_AppId != appId) {
    Console.Error.WriteLine($"Steam initialized for unexpected App ID {SteamUtils.GetAppID().m_AppId}.");
    SteamAPI.Shutdown();
    return false;
  }
  return true;
}

static IReadOnlyList<ItemDetails> QueryUserItems(AccountID_t accountId, uint appId) {
  var query = SteamUGC.CreateQueryUserUGCRequest(
      accountId,
      EUserUGCList.k_EUserUGCList_Published,
      EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
      EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc,
      new AppId_t(appId),
      new AppId_t(appId),
      1);
  if (query == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException("Could not create owner-item query.");
  }
  try {
    return RunQuery(query);
  }
  finally {
    SteamUGC.ReleaseQueryUGCRequest(query);
  }
}

static ItemDetails QueryItem(ulong publishedFileId) {
  var ids = new[] { new PublishedFileId_t(publishedFileId) };
  var query = SteamUGC.CreateQueryUGCDetailsRequest(ids, (uint)ids.Length);
  if (query == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException("Could not create identity-verification query.");
  }
  try {
    var results = RunQuery(query);
    return results.SingleOrDefault(item => item.PublishedFileId == publishedFileId)
        ?? throw new InvalidOperationException("Created item was not returned by live verification query.");
  }
  finally {
    SteamUGC.ReleaseQueryUGCRequest(query);
  }
}

static IReadOnlyList<ItemDetails> RunQuery(UGCQueryHandle_t query) {
  SteamUGC.SetReturnLongDescription(query, true);
  var completed = false;
  var ioFailureResult = false;
  var result = EResult.k_EResultNone;
  uint returned = 0;
  var call = SteamUGC.SendQueryUGCRequest(query);
  var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
  callResult.Set(call, (response, ioFailure) => {
    completed = true;
    ioFailureResult = ioFailure;
    result = response.m_eResult;
    returned = response.m_unNumResultsReturned;
  });
  WaitForCallback(() => completed, "Workshop query");
  if (ioFailureResult || result != EResult.k_EResultOK) {
    throw new InvalidOperationException($"Workshop query failed: result={result}, ioFailure={ioFailureResult}.");
  }

  var items = new List<ItemDetails>();
  for (uint i = 0; i < returned; ++i) {
    if (!SteamUGC.GetQueryUGCResult(query, i, out var details)) {
      throw new InvalidOperationException($"Could not read Workshop query result {i}.");
    }
    items.Add(new ItemDetails(
        details.m_nPublishedFileId.m_PublishedFileId,
        details.m_nConsumerAppID.m_AppId,
        details.m_rgchTitle,
        ToVisibility(details.m_eVisibility)));
  }
  return items;
}

static CreatedItem CreateItem(uint appId) {
  var completed = false;
  var ioFailureResult = false;
  var result = EResult.k_EResultNone;
  ulong publishedFileId = 0;
  var needsAgreement = false;
  var call = SteamUGC.CreateItem(new AppId_t(appId), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
  var callResult = CallResult<CreateItemResult_t>.Create();
  callResult.Set(call, (response, ioFailure) => {
    completed = true;
    ioFailureResult = ioFailure;
    result = response.m_eResult;
    publishedFileId = response.m_nPublishedFileId.m_PublishedFileId;
    needsAgreement = response.m_bUserNeedsToAcceptWorkshopLegalAgreement;
  });
  WaitForCallback(() => completed, "CreateItem");
  if (ioFailureResult || result != EResult.k_EResultOK || publishedFileId == 0) {
    throw new InvalidOperationException(
        $"CreateItem failed: result={result}, ioFailure={ioFailureResult}, id={publishedFileId}.");
  }
  return new CreatedItem(publishedFileId, needsAgreement);
}

static void UpdateItemProfile(
    ulong publishedFileId, CreationPlan plan, string description, string previewPath) {
  var handle = SteamUGC.StartItemUpdate(
      new AppId_t(plan.AppId), new PublishedFileId_t(publishedFileId));
  if (handle == UGCUpdateHandle_t.Invalid) {
    throw new InvalidOperationException("StartItemUpdate returned an invalid handle.");
  }
  if (!SteamUGC.SetItemTitle(handle, plan.Title) ||
      !SteamUGC.SetItemDescription(handle, description) ||
      !SteamUGC.SetItemVisibility(handle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate) ||
      !SteamUGC.SetItemTags(handle, plan.Tags) ||
      !SteamUGC.SetItemPreview(handle, previewPath)) {
    throw new InvalidOperationException("One or more profile metadata setters returned false.");
  }

  var completed = false;
  var ioFailureResult = false;
  var result = EResult.k_EResultNone;
  var call = SteamUGC.SubmitItemUpdate(handle, "Create private Workshop identity");
  var callResult = CallResult<SubmitItemUpdateResult_t>.Create();
  callResult.Set(call, (response, ioFailure) => {
    completed = true;
    ioFailureResult = ioFailure;
    result = response.m_eResult;
  });
  WaitForCallback(() => completed, "SubmitItemUpdate");
  if (ioFailureResult || result != EResult.k_EResultOK) {
    throw new InvalidOperationException(
        $"SubmitItemUpdate failed: result={result}, ioFailure={ioFailureResult}.");
  }
}

static void WaitForCallback(Func<bool> completed, string operation) {
  var deadline = DateTime.UtcNow.AddSeconds(120);
  while (!completed() && DateTime.UtcNow < deadline) {
    SteamAPI.RunCallbacks();
    Thread.Sleep(100);
  }
  if (!completed()) {
    throw new TimeoutException($"Timed out waiting for {operation} callback.");
  }
}

static string ToVisibility(ERemoteStoragePublishedFileVisibility visibility) {
  return visibility switch {
    ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate => "Private",
    ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly => "FriendsOnly",
    ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted => "Unlisted",
    ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic => "Public",
    _ => visibility.ToString(),
  };
}

sealed record CreationPlan(
    uint AppId,
    string Title,
    string Visibility,
    string[] Tags,
    string DescriptionPath,
    string PreviewPath,
    string ResultPath);

sealed record CreatedItem(ulong PublishedFileId, bool NeedsLegalAgreement);

sealed record ItemDetails(ulong PublishedFileId, uint ConsumerAppId, string Title, string Visibility);

sealed record CreationResult(
    ulong PublishedFileId,
    uint AppId,
    ulong SteamId,
    string Title,
    string Visibility,
    bool NeedsLegalAgreement,
    DateTimeOffset CreatedAtUtc);

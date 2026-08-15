using System.Text.Json;
using Steamworks;

namespace IgorZ.TimberbornMods.Tools.WorkshopItemCreation;

sealed class WorkshopItemCreator {
  const uint TimberbornAppId = 1062090;

  sealed class CreationPlan {
    /// <summary>Creates a validated description of the private Workshop identity to create.</summary>
    public CreationPlan(
        uint appId, string title, string visibility, string[] tags,
        string descriptionPath, string previewPath, string resultPath) {
      AppId = appId;
      Title = title;
      Visibility = visibility;
      Tags = tags;
      DescriptionPath = descriptionPath;
      PreviewPath = previewPath;
      ResultPath = resultPath;
    }

    /// <summary>The Steam application that will own the new Workshop identity.</summary>
    public uint AppId { get; }

    /// <summary>The exact Workshop title used both for creation and duplicate detection.</summary>
    public string Title { get; }

    /// <summary>The requested initial visibility, currently restricted to Private.</summary>
    public string Visibility { get; }

    /// <summary>The complete initial Steam tag set.</summary>
    public string[] Tags { get; }

    /// <summary>The path to the text file used as the Workshop description.</summary>
    public string DescriptionPath { get; }

    /// <summary>The path to the image used as the primary Workshop preview.</summary>
    public string PreviewPath { get; }

    /// <summary>The path where the verified creation receipt must be written.</summary>
    public string ResultPath { get; }
  }

  sealed record CreatedItem(ulong PublishedFileId, bool NeedsLegalAgreement);

  sealed record PreparedPlan(
      CreationPlan Plan, string DescriptionPath, string Description, string PreviewPath, string ResultPath);

  sealed record ItemDetails(
      ulong PublishedFileId, uint ConsumerAppId, ulong OwnerSteamId, string Title, string Visibility);

  sealed class CreationResult {
    /// <summary>Creates the durable receipt written only after live verification succeeds.</summary>
    public CreationResult(
        ulong publishedFileId, uint appId, ulong steamId, string title, string visibility,
        bool needsLegalAgreement, DateTimeOffset createdAtUtc) {
      PublishedFileId = publishedFileId;
      AppId = appId;
      SteamId = steamId;
      Title = title;
      Visibility = visibility;
      NeedsLegalAgreement = needsLegalAgreement;
      CreatedAtUtc = createdAtUtc;
    }

    /// <summary>The immutable Steam Workshop identity returned by CreateItem.</summary>
    public ulong PublishedFileId { get; }

    /// <summary>The Steam application owning the Workshop identity.</summary>
    public uint AppId { get; }

    /// <summary>The Steam user that created the identity.</summary>
    public ulong SteamId { get; }

    /// <summary>The title observed during live verification.</summary>
    public string Title { get; }

    /// <summary>The visibility observed during live verification.</summary>
    public string Visibility { get; }

    /// <summary>Whether Steam reported that the creator must accept the Workshop legal agreement.</summary>
    public bool NeedsLegalAgreement { get; }

    /// <summary>The UTC time when verification completed and this receipt was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; }
  }

  static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(120);

  /// <summary>Validates a creation plan and optionally creates and verifies its private Workshop identity.</summary>
  public int Run(bool create, string planPathArgument) {
    var prepared = PreparePlan(planPathArgument);
    WritePlan(
        prepared.Plan, prepared.DescriptionPath, prepared.PreviewPath, prepared.ResultPath);
    if (!InitializeSteam(prepared.Plan.AppId)) {
      return 3;
    }

    try {
      var steamId = SteamUser.GetSteamID();
      Console.WriteLine($"  Steam user: {steamId.m_SteamID}");
      Console.WriteLine($"  Logged on: {SteamUser.BLoggedOn()}");

      var existingItems = QueryUserItems(steamId.GetAccountID(), prepared.Plan.AppId);
      var duplicate = existingItems.FirstOrDefault(item =>
          string.Equals(item.Title, prepared.Plan.Title, StringComparison.OrdinalIgnoreCase));
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

      var created = CreateItem(prepared.Plan.AppId);
      Console.WriteLine($"Created Workshop identity: {created.PublishedFileId}");
      Console.WriteLine($"Needs legal agreement: {created.NeedsLegalAgreement}");
      return CompleteCreatedItem(created, prepared, steamId.m_SteamID);
    } finally {
      SteamAPI.Shutdown();
    }
  }

  /// <summary>Completes and verifies a known partial identity without creating another Workshop item.</summary>
  public int Recover(ulong publishedFileId, bool needsLegalAgreement, string planPathArgument) {
    var prepared = PreparePlan(planPathArgument);
    WritePlan(
        prepared.Plan, prepared.DescriptionPath, prepared.PreviewPath, prepared.ResultPath);
    Console.WriteLine($"Recovery-only PublishedFileId: {publishedFileId}");
    Console.WriteLine("CreateItem: disabled in recovery mode");
    if (!InitializeSteam(prepared.Plan.AppId)) {
      return 3;
    }

    try {
      var steamId = SteamUser.GetSteamID().m_SteamID;
      var current = QueryItem(publishedFileId);
      if (current.ConsumerAppId != prepared.Plan.AppId || current.OwnerSteamId != steamId
          || current.Visibility != "Private"
          || current.Title.Length > 0
              && !string.Equals(current.Title, prepared.Plan.Title, StringComparison.Ordinal)) {
        throw new InvalidOperationException(
            $"Recovery target mismatch. App={current.ConsumerAppId}, Owner={current.OwnerSteamId}, "
            + $"Title={current.Title}, Visibility={current.Visibility}.");
      }

      return CompleteCreatedItem(
          new CreatedItem(publishedFileId, needsLegalAgreement), prepared, steamId);
    } finally {
      SteamAPI.Shutdown();
    }
  }

  int CompleteCreatedItem(
      CreatedItem created, PreparedPlan prepared, ulong steamId) {
    try {
      UpdateItemProfile(
          created.PublishedFileId, prepared.Plan, prepared.Description, prepared.PreviewPath);
      var verified = QueryItem(created.PublishedFileId);
      if (verified.ConsumerAppId != prepared.Plan.AppId || verified.OwnerSteamId != steamId
          || !string.Equals(verified.Title, prepared.Plan.Title, StringComparison.Ordinal)
          || verified.Visibility != prepared.Plan.Visibility) {
        throw new InvalidOperationException(
            $"Live verification mismatch. App={verified.ConsumerAppId}, Owner={verified.OwnerSteamId}, "
            + $"Title={verified.Title}, Visibility={verified.Visibility}.");
      }

      Directory.CreateDirectory(Path.GetDirectoryName(prepared.ResultPath)!);
      var result = new CreationResult(
          created.PublishedFileId, prepared.Plan.AppId, steamId, verified.Title, verified.Visibility,
          created.NeedsLegalAgreement, DateTimeOffset.UtcNow);
      File.WriteAllText(prepared.ResultPath, JsonSerializer.Serialize(result, JsonOptions()));
      Console.WriteLine($"Verified Workshop identity: {created.PublishedFileId}");
      Console.WriteLine($"CREATED_PUBLISHED_FILE_ID={created.PublishedFileId}");
      return 0;
    } catch (Exception exception) {
      // Steam identities cannot be rolled back here, so preserve the ID even when later profile setup fails.
      Console.Error.WriteLine(
          $"Workshop identity {created.PublishedFileId} was created, but profile update or verification failed.");
      Console.Error.WriteLine(exception);
      Console.Error.WriteLine($"PARTIAL_PUBLISHED_FILE_ID={created.PublishedFileId}");
      return 5;
    }
  }

  static PreparedPlan PreparePlan(string planPathArgument) {
    var planPath = Path.GetFullPath(planPathArgument);
    if (!File.Exists(planPath)) {
      throw new FileNotFoundException("Creation plan not found.", planPath);
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

    return new PreparedPlan(
        plan, descriptionPath, File.ReadAllText(descriptionPath), previewPath, resultPath);
  }

  static void WritePlan(CreationPlan plan, string descriptionPath, string previewPath, string resultPath) {
    Console.WriteLine("Steam Workshop identity creation plan");
    Console.WriteLine($"  App ID: {plan.AppId}");
    Console.WriteLine($"  Title: {plan.Title}");
    Console.WriteLine($"  Visibility: {plan.Visibility}");
    Console.WriteLine($"  Tags: {string.Join(", ", plan.Tags)}");
    Console.WriteLine($"  Description: {descriptionPath}");
    Console.WriteLine($"  Preview: {previewPath}");
    Console.WriteLine("  Content upload: disabled (this tool has no content-folder input)");
    Console.WriteLine($"  Result: {resultPath}");
  }

  static JsonSerializerOptions JsonOptions() {
    return new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };
  }

  static void ValidatePlan(CreationPlan plan) {
    if (plan.AppId != TimberbornAppId) {
      throw new InvalidOperationException($"Only Timberborn App ID {TimberbornAppId} is supported, got {plan.AppId}.");
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
    if (string.IsNullOrWhiteSpace(plan.DescriptionPath)
        || string.IsNullOrWhiteSpace(plan.PreviewPath)
        || string.IsNullOrWhiteSpace(plan.ResultPath)) {
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

  IReadOnlyList<ItemDetails> QueryUserItems(AccountID_t accountId, uint appId) {
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
    } finally {
      SteamUGC.ReleaseQueryUGCRequest(query);
    }
  }

  ItemDetails QueryItem(ulong publishedFileId) {
    var ids = new[] { new PublishedFileId_t(publishedFileId) };
    var query = SteamUGC.CreateQueryUGCDetailsRequest(ids, (uint) ids.Length);
    if (query == UGCQueryHandle_t.Invalid) {
      throw new InvalidOperationException("Could not create identity-verification query.");
    }
    try {
      var results = RunQuery(query);
      return results.SingleOrDefault(item => item.PublishedFileId == publishedFileId)
          ?? throw new InvalidOperationException("Created item was not returned by live verification query.");
    } finally {
      SteamUGC.ReleaseQueryUGCRequest(query);
    }
  }

  IReadOnlyList<ItemDetails> RunQuery(UGCQueryHandle_t query) {
    SteamUGC.SetReturnLongDescription(query, true);
    var completed = false;
    var ioFailureResult = false;
    var result = EResult.k_EResultNone;
    uint returned = 0;
    var call = SteamUGC.SendQueryUGCRequest(query);
    using var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
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
    for (uint index = 0; index < returned; index++) {
      if (!SteamUGC.GetQueryUGCResult(query, index, out var details)) {
        throw new InvalidOperationException($"Could not read Workshop query result {index}.");
      }
      items.Add(new ItemDetails(
          details.m_nPublishedFileId.m_PublishedFileId, details.m_nConsumerAppID.m_AppId,
          details.m_ulSteamIDOwner, details.m_rgchTitle, ToVisibility(details.m_eVisibility)));
    }
    return items;
  }

  CreatedItem CreateItem(uint appId) {
    var completed = false;
    var ioFailureResult = false;
    var result = EResult.k_EResultNone;
    ulong publishedFileId = 0;
    var needsAgreement = false;
    var call = SteamUGC.CreateItem(new AppId_t(appId), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
    using var callResult = CallResult<CreateItemResult_t>.Create();
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

  void UpdateItemProfile(
      ulong publishedFileId, CreationPlan plan, string description, string previewPath) {
    var handle = SteamUGC.StartItemUpdate(new AppId_t(plan.AppId), new PublishedFileId_t(publishedFileId));
    if (handle == UGCUpdateHandle_t.Invalid) {
      throw new InvalidOperationException("StartItemUpdate returned an invalid handle.");
    }
    if (!SteamUGC.SetItemTitle(handle, plan.Title)
        || !SteamUGC.SetItemDescription(handle, description)
        || !SteamUGC.SetItemVisibility(
            handle, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate)
        || !SteamUGC.SetItemTags(handle, plan.Tags)
        || !SteamUGC.SetItemPreview(handle, previewPath)) {
      throw new InvalidOperationException("One or more profile metadata setters returned false.");
    }

    var completed = false;
    var ioFailureResult = false;
    var result = EResult.k_EResultNone;
    var call = SteamUGC.SubmitItemUpdate(handle, "Create private Workshop identity");
    using var callResult = CallResult<SubmitItemUpdateResult_t>.Create();
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
    var deadline = DateTime.UtcNow.Add(CallbackTimeout);
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
}

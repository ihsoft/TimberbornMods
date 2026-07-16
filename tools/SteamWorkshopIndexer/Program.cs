using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steamworks;

const uint AppId = 1062090;

var options = Options.Parse(args);
if (options is null) {
  return 2;
}

if (!InitializeSteam()) {
  return 3;
}

try {
  var outputPath = Path.GetFullPath(options.OutputPath);
  Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

  var records = new List<WorkshopRecord>();
  var page = options.StartPage;
  uint pagesProcessed = 0;
  uint totalMatching = 0;
  while (options.MaxPages == 0 || pagesProcessed < options.MaxPages) {
    var result = QueryPage(page, options.Language);
    totalMatching = result.TotalMatching;
    if (result.Items.Count == 0) {
      break;
    }

    records.AddRange(result.Items.Select(Classify));
    pagesProcessed++;
    Console.WriteLine($"Page {page}: {result.Items.Count} items, {records.Count} collected in this run.");
    if (page * 50 >= totalMatching) {
      break;
    }
    page++;
  }

  if (options.Append && File.Exists(outputPath)) {
    records.AddRange(ReadJsonLines(outputPath));
  }
  records = records.GroupBy(record => record.PublishedFileId).Select(group => group.First())
      .OrderByDescending(record => record.UpdatedAtUtc).ToList();
  WriteJsonLines(outputPath, records);
  WriteSummary(outputPath, records, totalMatching, options.Language);
  Console.WriteLine($"Wrote {records.Count} Workshop items to {outputPath}");
  return 0;
}
catch (Exception exception) {
  Console.Error.WriteLine(exception.Message);
  return 4;
}
finally {
  SteamAPI.Shutdown();
}

static bool InitializeSteam() {
  Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
  Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());
  if (!Packsize.Test() || !DllCheck.Test()) {
    Console.Error.WriteLine("Steamworks.NET platform checks failed.");
    return false;
  }
  if (!SteamAPI.Init()) {
    Console.Error.WriteLine("SteamAPI.Init failed. Start Steam and make sure steam_appid.txt is present.");
    return false;
  }
  if (!SteamUser.BLoggedOn()) {
    Console.Error.WriteLine("Steam is running, but the current user is not logged on.");
    return false;
  }
  return true;
}

static QueryPageResult QueryPage(uint page, string language) {
  var handle = SteamUGC.CreateQueryAllUGCRequest(
      EUGCQuery.k_EUGCQuery_RankedByLastUpdatedDate,
      EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
      new AppId_t(AppId), new AppId_t(AppId), page);
  if (handle == UGCQueryHandle_t.Invalid) {
    throw new InvalidOperationException($"Steam returned an invalid query handle for page {page}.");
  }

  try {
    RequireQueryOption(SteamUGC.SetReturnLongDescription(handle, true), "long descriptions");
    RequireQueryOption(SteamUGC.SetReturnAdditionalPreviews(handle, true), "additional previews");
    RequireQueryOption(SteamUGC.SetLanguage(handle, language), $"language '{language}'");
    RequireQueryOption(SteamUGC.SetAllowCachedResponse(handle, 300), "cached responses");

    var completed = false;
    var ioFailure = false;
    SteamUGCQueryCompleted_t callbackResult = default;
    var callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
    callResult.Set(SteamUGC.SendQueryUGCRequest(handle), (result, failed) => {
      callbackResult = result;
      ioFailure = failed;
      completed = true;
    });

    var deadline = DateTime.UtcNow.AddSeconds(60);
    while (!completed && DateTime.UtcNow < deadline) {
      SteamAPI.RunCallbacks();
      Thread.Sleep(50);
    }
    if (!completed) {
      throw new TimeoutException($"Timed out querying Steam Workshop page {page}.");
    }
    if (ioFailure || callbackResult.m_eResult != EResult.k_EResultOK) {
      throw new InvalidOperationException(
          $"Steam Workshop query failed on page {page}: {callbackResult.m_eResult}, ioFailure={ioFailure}.");
    }

    var items = new List<RawWorkshopRecord>();
    for (uint index = 0; index < callbackResult.m_unNumResultsReturned; index++) {
      if (!SteamUGC.GetQueryUGCResult(handle, index, out var details)) {
        Console.Error.WriteLine($"Steam did not return details for page {page}, index {index}; skipping item.");
        continue;
      }
      items.Add(ToRecord(handle, index, details));
    }
    return new QueryPageResult(items, callbackResult.m_unTotalMatchingResults);
  }
  finally {
    SteamUGC.ReleaseQueryUGCRequest(handle);
  }
}

static void RequireQueryOption(bool success, string option) {
  if (!success) {
    throw new InvalidOperationException($"Steam rejected the query option for {option}.");
  }
}

static RawWorkshopRecord ToRecord(UGCQueryHandle_t handle, uint index, SteamUGCDetails_t details) {
  SteamUGC.GetQueryUGCPreviewURL(handle, index, out var previewUrl, 2048);

  var tags = new List<string>();
  var tagCount = SteamUGC.GetQueryUGCNumTags(handle, index);
  for (uint tagIndex = 0; tagIndex < tagCount; tagIndex++) {
    if (SteamUGC.GetQueryUGCTag(handle, index, tagIndex, out var tag, 256)) {
      tags.Add(tag);
    }
  }

  return new RawWorkshopRecord(
      details.m_nPublishedFileId.m_PublishedFileId.ToString(), details.m_rgchTitle, details.m_rgchDescription,
      details.m_ulSteamIDOwner.ToString(), details.m_rtimeCreated, details.m_rtimeUpdated, previewUrl,
      tags, details.m_unVotesUp, details.m_unVotesDown, details.m_flScore);
}

static WorkshopRecord Classify(RawWorkshopRecord item) {
  var searchableText = NormalizeSearchText(item.Title + "\n" + item.Description + "\n" + string.Join(' ', item.Tags));
  var matches = CategoryRules.All.Select(rule => MatchRule(rule, searchableText, item.Tags)).Where(match => match.Score > 0)
      .OrderByDescending(match => match.Score).ThenBy(match => match.Category).ToList();
  var primaryCategory = matches.FirstOrDefault()?.Category ?? "other";
  var plainDescription = StripSteamMarkup(item.Description);
  return new WorkshopRecord(
      item.PublishedFileId, item.Title, item.Description, plainDescription, item.CreatorSteamId,
      DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt).UtcDateTime,
      DateTimeOffset.FromUnixTimeSeconds(item.UpdatedAt).UtcDateTime, item.PreviewUrl, item.Tags,
      item.VotesUp, item.VotesDown, item.Score, primaryCategory, matches);
}

static CategoryMatch MatchRule(CategoryRule rule, string searchableText, IReadOnlyList<string> tags) {
  var evidence = new List<string>();
  var score = 0;
  foreach (var tag in tags) {
    if (rule.Tags.Any(candidate => string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase))) {
      evidence.Add($"tag:{tag}");
      score += 5;
    }
  }
  foreach (var term in rule.Terms) {
    if (Regex.IsMatch(searchableText, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])")) {
      evidence.Add($"term:{term}");
      score++;
    }
  }
  return new CategoryMatch(rule.Name, score, evidence);
}

static string NormalizeSearchText(string value) {
  return Regex.Replace(value.ToLowerInvariant(), @"\s+", " ");
}

static string StripSteamMarkup(string value) {
  var withoutMarkup = Regex.Replace(value, @"\[/?[^\]]+\]", " ");
  return Regex.Replace(withoutMarkup, @"\s+", " ").Trim();
}

static void WriteJsonLines(string outputPath, IEnumerable<WorkshopRecord> records) {
  var jsonOptions = JsonOptions();
  using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
  foreach (var record in records) {
    writer.WriteLine(JsonSerializer.Serialize(record, jsonOptions));
  }
}

static IEnumerable<WorkshopRecord> ReadJsonLines(string outputPath) {
  foreach (var line in File.ReadLines(outputPath)) {
    if (!string.IsNullOrWhiteSpace(line)) {
      yield return JsonSerializer.Deserialize<WorkshopRecord>(line, JsonOptions())
          ?? throw new InvalidDataException($"Could not parse an existing record in {outputPath}.");
    }
  }
}

static JsonSerializerOptions JsonOptions() {
  return new JsonSerializerOptions() {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      PropertyNameCaseInsensitive = true,
  };
}

static void WriteSummary(
    string outputPath, IReadOnlyList<WorkshopRecord> records, uint totalMatching, string language) {
  var categoryCounts = records.GroupBy(record => record.PrimaryCategory)
      .OrderByDescending(group => group.Count()).ToDictionary(group => group.Key, group => group.Count());
  var summary = new {
    generated_at_utc = DateTime.UtcNow,
    app_id = AppId,
    language,
    collected_items = records.Count,
    steam_total_matching = totalMatching,
    primary_category_counts = categoryCounts,
  };
  var summaryPath = Path.ChangeExtension(outputPath, ".summary.json");
  File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions() { WriteIndented = true }));
}

record Options(string OutputPath, string Language, uint StartPage, uint MaxPages, bool Append) {
  public static Options? Parse(string[] args) {
    var output = Path.Combine(".tools", "workshop-index", "timberborn-workshop-bootstrap.jsonl");
    var language = "english";
    uint startPage = 1;
    uint maxPages = 0;
    var append = false;
    for (var index = 0; index < args.Length; index++) {
      switch (args[index]) {
        case "--output" when index + 1 < args.Length:
          output = args[++index];
          break;
        case "--language" when index + 1 < args.Length:
          language = args[++index];
          break;
        case "--max-pages" when index + 1 < args.Length && uint.TryParse(args[++index], out var parsed):
          maxPages = parsed;
          break;
        case "--start-page" when index + 1 < args.Length && uint.TryParse(args[++index], out var parsed):
          startPage = parsed;
          break;
        case "--append":
          append = true;
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
    return new Options(output, language, startPage, maxPages, append);
  }

  static void PrintUsage() {
    Console.WriteLine(
        "SteamWorkshopIndexer [--output <jsonl>] [--language <steam-language>] [--start-page <page>] "
            + "[--max-pages <count>] [--append]");
  }
}

record QueryPageResult(List<RawWorkshopRecord> Items, uint TotalMatching);
record RawWorkshopRecord(
    string PublishedFileId, string Title, string Description, string CreatorSteamId, uint CreatedAt, uint UpdatedAt,
    string PreviewUrl, List<string> Tags, uint VotesUp, uint VotesDown, float Score);
record WorkshopRecord(
    string PublishedFileId, string Title, string DescriptionRaw, string DescriptionPlain, string CreatorSteamId,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string PreviewUrl, List<string> Tags, uint VotesUp, uint VotesDown,
    float Score, string PrimaryCategory, List<CategoryMatch> Categories);
record CategoryMatch(string Category, int Score, List<string> Evidence);
record CategoryRule(string Name, string[] Tags, string[] Terms);

static class CategoryRules {
  public static readonly CategoryRule[] All = [
    new("map", ["Maps", "Map"], ["map", "maps", "terrain", "starting location", "challenge map"]),
    new("buildings", ["Buildings", "Building"], [
      "building", "buildings", "structure", "structures", "monument", "storage", "workplace", "housing",
    ]),
    new("qol", ["QoL", "Quality of Life", "UI"], [
      "quality of life", "qol", "interface", "ui", "hotkey", "shortcut", "overlay", "tooltip", "management",
    ]),
    new("faction", ["Faction", "Factions"], [
      "faction", "factions", "folktails", "iron teeth", "new faction", "custom faction",
    ]),
  ];
}

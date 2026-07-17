using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const uint AppId = 1062090;
const int DetailsBatchSize = 100;

var options = Options.Parse(args);
if (options is null) {
  return 2;
}

try {
  var outputPath = Path.GetFullPath(options.OutputPath);
  var previewDirectory = Path.GetFullPath(options.PreviewDirectory);
  Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
  if (!options.SkipPreviews) {
    Directory.CreateDirectory(previewDirectory);
  }

  var existingRecords = File.Exists(outputPath)
      ? ReadJsonLines(outputPath).ToDictionary(record => record.PublishedFileId)
      : new Dictionary<string, WorkshopRecord>();
  using var httpClient = CreateHttpClient();
  var previewCache = new PreviewCache(
      httpClient, previewDirectory, options.MaxPreviewCacheBytes, existingRecords);
  var records = new List<WorkshopRecord>();
  var seenIds = new HashSet<string>();
  uint totalMatching = 0;
  uint pagesProcessed = 0;
  for (var page = options.StartPage; options.MaxPages == 0 || pagesProcessed < options.MaxPages; page++) {
    var browsePage = await QueryBrowsePageAsync(httpClient, page, options.DelayMilliseconds);
    if (browsePage.TotalMatching > 0) {
      totalMatching = browsePage.TotalMatching;
    }

    var newIds = browsePage.PublishedFileIds.Where(seenIds.Add).ToList();
    if (newIds.Count == 0) {
      break;
    }

    foreach (var batch in newIds.Chunk(DetailsBatchSize)) {
      var items = await QueryDetailsAsync(httpClient, batch, options.DelayMilliseconds);
      foreach (var item in items) {
        var classified = Classify(item);
        if (!options.SkipPreviews && !string.IsNullOrWhiteSpace(classified.PreviewUrl)) {
          classified = classified with {
            PreviewCachePath = await previewCache.GetAsync(classified, options.DelayMilliseconds),
          };
        }
        records.Add(classified);
      }
    }

    pagesProcessed++;
    Console.WriteLine(
        $"Page {page}: {newIds.Count} IDs, {records.Count} detailed items, "
            + $"preview cache {previewCache.CacheBytes} bytes.");
    if (browsePage.IsLastPage) {
      break;
    }
  }

  if (options.Append) {
    records.AddRange(existingRecords.Values);
  }
  records = records.GroupBy(record => record.PublishedFileId).Select(group => group.First())
      .OrderByDescending(record => record.UpdatedAtUtc).ToList();
  WriteJsonLines(outputPath, records);
  WriteSummary(outputPath, records, totalMatching, options, previewCache.CacheBytes);
  Console.WriteLine($"Wrote {records.Count} Workshop items to {outputPath}");
  return 0;
}
catch (Exception exception) {
  Console.Error.WriteLine(exception.Message);
  return 4;
}

static HttpClient CreateHttpClient() {
  var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(60) };
  client.DefaultRequestHeaders.UserAgent.ParseAdd("TimberbornMods-PublicWorkshopIndexer/1.0");
  return client;
}

static async Task<BrowsePageResult> QueryBrowsePageAsync(HttpClient client, uint page, int delayMilliseconds) {
  await DelayAsync(delayMilliseconds);
  var url = "https://steamcommunity.com/workshop/browse/"
      + $"?appid={AppId}&browsesort=lastupdated&section=readytouseitems&actualsort=lastupdated&p={page}&l=english";
  var html = await client.GetStringAsync(url);
  var ids = Regex.Matches(html, @"publishedfileid\\+"":\\+""(\d+)")
      .Select(match => match.Groups[1].Value).Distinct().ToList();
  if (ids.Count == 0) {
    ids = Regex.Matches(html, @"data-publishedfileid=&quot;(\d+)&quot;|data-publishedfileid=""(\d+)""")
      .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
      .Distinct().ToList();
  }
  if (ids.Count == 0) {
    ids = Regex.Matches(html, @"sharedfiles/filedetails/\?id=(\d+)")
        .Select(match => match.Groups[1].Value).Distinct().ToList();
  }

  var totalMatching = ParseTotalMatching(html);
  const uint itemsPerPage = 30;
  var isLastPage = totalMatching > 0 && page * itemsPerPage >= totalMatching;
  return new BrowsePageResult(ids, totalMatching, isLastPage);
}

static uint ParseTotalMatching(string html) {
  var match = Regex.Match(html, @"total_count.{0,20}?:(\d+)", RegexOptions.IgnoreCase);
  return match.Success && uint.TryParse(match.Groups[1].Value, out var total) ? total : 0;
}

static async Task<List<RawWorkshopRecord>> QueryDetailsAsync(
    HttpClient client, IReadOnlyList<string> publishedFileIds, int delayMilliseconds) {
  await DelayAsync(delayMilliseconds);
  var values = new List<KeyValuePair<string, string>> {
    new("itemcount", publishedFileIds.Count.ToString()),
    new("include_tags", "true"),
  };
  for (var index = 0; index < publishedFileIds.Count; index++) {
    values.Add(new KeyValuePair<string, string>($"publishedfileids[{index}]", publishedFileIds[index]));
  }

  using var response = await client.PostAsync(
      "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
      new FormUrlEncodedContent(values));
  response.EnsureSuccessStatusCode();
  await using var stream = await response.Content.ReadAsStreamAsync();
  using var document = await JsonDocument.ParseAsync(stream);
  var results = new List<RawWorkshopRecord>();
  foreach (var item in document.RootElement.GetProperty("response").GetProperty("publishedfiledetails").EnumerateArray()) {
    if (GetUInt32(item, "result") != 1) {
      continue;
    }
    var tags = item.TryGetProperty("tags", out var tagsElement)
        ? tagsElement.EnumerateArray().Select(tag => GetString(tag, "tag")).Where(tag => tag.Length > 0).ToList()
        : [];
    results.Add(new RawWorkshopRecord(
        GetString(item, "publishedfileid"), GetString(item, "title"), GetString(item, "description"),
        GetString(item, "creator"), GetUInt32(item, "time_created"), GetUInt32(item, "time_updated"),
        GetString(item, "preview_url"), tags, GetUInt32(item, "votes_up"), GetUInt32(item, "votes_down"),
        GetSingle(item, "score")));
  }
  return results;
}

static string GetString(JsonElement item, string property) {
  if (!item.TryGetProperty(property, out var value)) {
    return string.Empty;
  }
  return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
}

static uint GetUInt32(JsonElement item, string property) {
  if (!item.TryGetProperty(property, out var value)) {
    return 0;
  }
  return value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var number)
      ? number
      : uint.TryParse(value.ToString(), out number) ? number : 0;
}

static float GetSingle(JsonElement item, string property) {
  if (!item.TryGetProperty(property, out var value)) {
    return 0;
  }
  return value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number)
      ? number
      : float.TryParse(value.ToString(), out number) ? number : 0;
}

static Task DelayAsync(int milliseconds) {
  return milliseconds > 0 ? Task.Delay(milliseconds) : Task.CompletedTask;
}

static WorkshopRecord Classify(RawWorkshopRecord item) {
  var searchableText = NormalizeSearchText(item.Title + "\n" + item.Description + "\n" + string.Join(' ', item.Tags));
  var matches = CategoryRules.All.Select(rule => MatchRule(rule, searchableText, item.Tags)).Where(match => match.Score > 0)
      .OrderByDescending(match => match.Score).ThenBy(match => match.Category).ToList();
  var primaryCategory = matches.FirstOrDefault()?.Category ?? "other";
  return new WorkshopRecord(
      item.PublishedFileId, item.Title, item.Description, StripSteamMarkup(item.Description), item.CreatorSteamId,
      DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt).UtcDateTime,
      DateTimeOffset.FromUnixTimeSeconds(item.UpdatedAt).UtcDateTime, item.PreviewUrl, null, item.Tags,
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
  return Regex.Replace(Regex.Replace(value, @"\[/?[^\]]+\]", " "), @"\s+", " ").Trim();
}

static void WriteJsonLines(string outputPath, IEnumerable<WorkshopRecord> records) {
  using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
  foreach (var record in records) {
    writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions()));
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
  return new JsonSerializerOptions {
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true,
  };
}

static void WriteSummary(
    string outputPath, IReadOnlyList<WorkshopRecord> records, uint totalMatching, Options options, long previewCacheBytes) {
  var summary = new {
    generated_at_utc = DateTime.UtcNow,
    app_id = AppId,
    source = "public-http",
    collected_items = records.Count,
    steam_total_matching = totalMatching,
    preview_cache_bytes = previewCacheBytes,
    preview_cache_limit_bytes = options.MaxPreviewCacheBytes,
    primary_category_counts = records.GroupBy(record => record.PrimaryCategory)
        .OrderByDescending(group => group.Count()).ToDictionary(group => group.Key, group => group.Count()),
  };
  File.WriteAllText(
      Path.ChangeExtension(outputPath, ".summary.json"),
      JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
}

sealed class PreviewCache {
  readonly HttpClient _client;
  readonly string _directory;
  readonly long _limitBytes;
  readonly IReadOnlyDictionary<string, WorkshopRecord> _existingRecords;

  public PreviewCache(
      HttpClient client, string directory, long limitBytes, IReadOnlyDictionary<string, WorkshopRecord> existingRecords) {
    _client = client;
    _directory = directory;
    _limitBytes = limitBytes;
    _existingRecords = existingRecords;
    CacheBytes = Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length)
        : 0;
  }

  public long CacheBytes { get; private set; }

  public async Task<string> GetAsync(WorkshopRecord record, int delayMilliseconds) {
    var relativePath = record.PublishedFileId + ".preview";
    var path = Path.Combine(_directory, relativePath);
    if (File.Exists(path)
        && _existingRecords.TryGetValue(record.PublishedFileId, out var existing)
        && string.Equals(existing.PreviewUrl, record.PreviewUrl, StringComparison.Ordinal)) {
      return relativePath;
    }

    if (delayMilliseconds > 0) {
      await Task.Delay(delayMilliseconds);
    }
    using var response = await _client.GetAsync(record.PreviewUrl, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();
    var contentLength = response.Content.Headers.ContentLength;
    if (contentLength is > 0 && CacheBytes + contentLength > _limitBytes) {
      throw new InvalidOperationException(
          $"Preview cache limit would be exceeded ({CacheBytes + contentLength} > {_limitBytes} bytes). "
              + "Increase --max-preview-cache-bytes explicitly after reviewing disk usage.");
    }

    var temporaryPath = path + ".tmp";
    var previousLength = File.Exists(path) ? new FileInfo(path).Length : 0;
    try {
      await using var input = await response.Content.ReadAsStreamAsync();
      await using var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None);
      var buffer = new byte[81920];
      long written = 0;
      while (true) {
        var read = await input.ReadAsync(buffer);
        if (read == 0) {
          break;
        }
        written += read;
        if (CacheBytes - previousLength + written > _limitBytes) {
          throw new InvalidOperationException(
              $"Preview cache limit would be exceeded while downloading {record.PublishedFileId}. "
                  + "Increase --max-preview-cache-bytes explicitly after reviewing disk usage.");
        }
        await output.WriteAsync(buffer.AsMemory(0, read));
      }
      output.Close();
      File.Move(temporaryPath, path, true);
      CacheBytes = CacheBytes - previousLength + written;
      return relativePath;
    }
    finally {
      if (File.Exists(temporaryPath)) {
        File.Delete(temporaryPath);
      }
    }
  }
}

record Options(
    string OutputPath, string PreviewDirectory, uint StartPage, uint MaxPages, bool Append, bool SkipPreviews,
    long MaxPreviewCacheBytes, int DelayMilliseconds) {
  public static Options? Parse(string[] args) {
    var output = Path.Combine(".tools", "workshop-index", "timberborn-workshop-bootstrap.jsonl");
    var previews = Path.Combine(".tools", "workshop-index", "previews");
    uint startPage = 1;
    uint maxPages = 0;
    var append = false;
    var skipPreviews = false;
    long maxPreviewCacheBytes = 5_000_000_000;
    var delayMilliseconds = 150;
    for (var index = 0; index < args.Length; index++) {
      switch (args[index]) {
        case "--output" when index + 1 < args.Length:
          output = args[++index];
          break;
        case "--preview-directory" when index + 1 < args.Length:
          previews = args[++index];
          break;
        case "--max-pages" when index + 1 < args.Length && uint.TryParse(args[++index], out var parsed):
          maxPages = parsed;
          break;
        case "--start-page" when index + 1 < args.Length && uint.TryParse(args[++index], out var parsed):
          startPage = parsed;
          break;
        case "--max-preview-cache-bytes" when index + 1 < args.Length && long.TryParse(args[++index], out var parsed):
          maxPreviewCacheBytes = parsed;
          break;
        case "--delay-ms" when index + 1 < args.Length && int.TryParse(args[++index], out var parsed):
          delayMilliseconds = parsed;
          break;
        case "--append":
          append = true;
          break;
        case "--skip-previews":
          skipPreviews = true;
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
    return new Options(
        output, previews, startPage, maxPages, append, skipPreviews, maxPreviewCacheBytes, delayMilliseconds);
  }

  static void PrintUsage() {
    Console.WriteLine(
        "SteamWorkshopIndexer [--output <jsonl>] [--preview-directory <directory>] [--start-page <page>] "
            + "[--max-pages <count>] [--append] [--skip-previews] [--max-preview-cache-bytes <bytes>] "
            + "[--delay-ms <milliseconds>]");
  }
}

record BrowsePageResult(List<string> PublishedFileIds, uint TotalMatching, bool IsLastPage);
record RawWorkshopRecord(
    string PublishedFileId, string Title, string Description, string CreatorSteamId, uint CreatedAt, uint UpdatedAt,
    string PreviewUrl, List<string> Tags, uint VotesUp, uint VotesDown, float Score);
record WorkshopRecord(
    string PublishedFileId, string Title, string DescriptionRaw, string DescriptionPlain, string CreatorSteamId,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, string PreviewUrl, string? PreviewCachePath, List<string> Tags,
    uint VotesUp, uint VotesDown, float Score, string PrimaryCategory, List<CategoryMatch> Categories);
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

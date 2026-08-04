using System.Text.RegularExpressions;

static class WorkshopCategoryClassifier {
  const string MapCategory = "map";
  const string MapTag = "Map";

  public static CategoryClassification Classify(
      string title, string description, IReadOnlyList<string> tags) {
    var searchableText = NormalizeSearchText(title + "\n" + description + "\n" + string.Join(' ', tags));
    var hasMapTag = tags.Any(tag => string.Equals(tag, MapTag, StringComparison.OrdinalIgnoreCase));
    var matches = CategoryRules.All
        .Select(rule => MatchRule(rule, searchableText, tags))
        .Where(match => match.Score > 0)
        .ToList();
    if (hasMapTag) {
      var actualTag = tags.First(tag => string.Equals(tag, MapTag, StringComparison.OrdinalIgnoreCase));
      matches.Add(new CategoryMatch(MapCategory, 5, [$"tag:{actualTag}"]));
    }
    matches = matches.OrderByDescending(match => match.Score).ThenBy(match => match.Category).ToList();
    var primaryCategory = hasMapTag
        ? MapCategory
        : matches.FirstOrDefault()?.Category ?? "other";
    return new CategoryClassification(primaryCategory, matches);
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
}

record CategoryClassification(string PrimaryCategory, List<CategoryMatch> Matches);
record CategoryMatch(string Category, int Score, List<string> Evidence);
record CategoryRule(string Name, string[] Tags, string[] Terms);

static class CategoryRules {
  public static readonly CategoryRule[] All = [
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

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("Map tag defines a map", MapTagDefinesMap),
      ("Map text does not define a map", MapTextDoesNotDefineMap),
      ("Map tag wins over other category evidence", MapTagWinsOverOtherEvidence),
      ("A similar tag does not replace the Map tag", SimilarTagDoesNotDefineMap),
  ];

  static int Main() {
    return TestRunner.Run(Tests);
  }

  static void MapTagDefinesMap() {
    var result = WorkshopCategoryClassifier.Classify("Untitled", "No description", ["map"]);

    Assert.Equal("map", result.PrimaryCategory);
    var mapMatch = result.Matches.Single(match => match.Category == "map");
    Assert.Equal(5, mapMatch.Score);
    Assert.Equal("tag:map", mapMatch.Evidence.Single());
  }

  static void MapTextDoesNotDefineMap() {
    var result = WorkshopCategoryClassifier.Classify(
        "Challenge map and terrain tools", "Edits a starting location and terrain.", ["Mod", "Modding tools"]);

    Assert.False(result.PrimaryCategory == "map");
    Assert.False(result.Matches.Any(match => match.Category == "map"));
  }

  static void MapTagWinsOverOtherEvidence() {
    var result = WorkshopCategoryClassifier.Classify(
        "Building and UI test", "Adds buildings, storage, hotkeys, and tooltips.", ["Map", "Buildings", "QoL"]);

    Assert.Equal("map", result.PrimaryCategory);
  }

  static void SimilarTagDoesNotDefineMap() {
    var result = WorkshopCategoryClassifier.Classify("Map collection", "Contains maps.", ["Maps"]);

    Assert.False(result.PrimaryCategory == "map");
    Assert.False(result.Matches.Any(match => match.Category == "map"));
  }
}

using System.IO.Compression;
using System.Text;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("Archive analysis counts only living log trees", CountsOnlyLivingLogTrees),
      ("Forest levels use five evenly spaced bands", UsesExpectedForestBands),
  ];

  static int Main() {
    return TestRunner.Run(Tests);
  }

  static void CountsOnlyLivingLogTrees() {
    using var archiveStream = new MemoryStream();
    using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true)) {
      WriteEntry(archive, "map_metadata.json", """{"Width":10,"Height":10}""");
      WriteEntry(archive, "world.json", """
          {"Entities":[
            {"Components":{"LivingNaturalResource":{},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"LivingNaturalResource":{"IsDead":false},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"LivingNaturalResource":{"IsDead":true},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"LivingNaturalResource":{},"Yielder:Gatherable":{"Yield":{"Good":"Berries"}}}},
            {"Components":{"LivingNaturalResource":{},"Yielder:Cuttable":{"Yield":{"Good":"Stone"}}}}
          ]}
          """);
    }
    archiveStream.Position = 0;

    using var archiveToRead = new ZipArchive(archiveStream, ZipArchiveMode.Read);
    var analysis = MapArchiveAnalyzer.Analyze(archiveToRead);
    var forest = analysis.Classifications[ForestDensityClassifier.FeatureKey];

    Assert.Equal(10, analysis.Width);
    Assert.Equal(10, analysis.Height);
    Assert.Equal(2L, forest.GetProperty("live_tree_count").GetInt64());
    Assert.Equal(0.02, forest.GetProperty("coverage_ratio").GetDouble());
    Assert.Equal(0, forest.GetProperty("level").GetInt32());
  }

  static void UsesExpectedForestBands() {
    Assert.Equal(0, ForestDensityClassifier.GetLevel(0.049999));
    Assert.Equal(1, ForestDensityClassifier.GetLevel(0.05));
    Assert.Equal(1, ForestDensityClassifier.GetLevel(0.199999));
    Assert.Equal(2, ForestDensityClassifier.GetLevel(0.20));
    Assert.Equal(2, ForestDensityClassifier.GetLevel(0.349999));
    Assert.Equal(3, ForestDensityClassifier.GetLevel(0.35));
    Assert.Equal(3, ForestDensityClassifier.GetLevel(0.50));
    Assert.Equal(4, ForestDensityClassifier.GetLevel(0.500001));
  }

  static void WriteEntry(ZipArchive archive, string name, string contents) {
    var entry = archive.CreateEntry(name);
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write(contents);
  }
}

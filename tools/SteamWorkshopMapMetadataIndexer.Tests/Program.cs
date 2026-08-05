using System.IO.Compression;
using System.Text;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("Archive analysis counts log trees unless explicitly dead", CountsOnlyLivingLogTrees),
      ("Archive analysis trusts runtime map size over stale metadata", TrustsRuntimeMapSize),
      ("Forest levels use five evenly spaced bands", UsesExpectedForestBands),
      ("Forest coverage excludes open surface water", ExcludesOpenWaterFromForestCoverage),
      ("Water form always emits a searchable concrete value", EmitsConcreteWaterForms),
      ("Water decoder reads legacy heights and excludes buried columns", DecodesLegacySurfaceWater),
      ("Water decoder reads voxel terrain", DecodesVoxelSurfaceWater),
      ("Lake diagnostics accept irregular connected shapes", DetectsIrregularLakeCore),
      ("River diagnostics exclude shallow lake shores", ExcludesLakeShoreFromRiverCandidates),
      ("Shallow lake diagnostics require a two-dimensional core", RequiresTwoDimensionalShallowLakeCore),
      ("Plateau levels use full confirmed plateau coverage", UsesExpectedPlateauBands),
      ("Plateau classifier accepts disconnected nearby heights as flat", AcceptsNearbyFlatHeights),
      ("Plateau classifier keeps separated terrain levels distinct", KeepsDistinctTerrainLevels),
      ("Plateau classifier excludes open water", ExcludesOpenWaterFromPlateaus),
  ];

  static int Main() {
    return TestRunner.Run(Tests);
  }

  static void CountsOnlyLivingLogTrees() {
    using var archiveStream = new MemoryStream();
    using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true)) {
      WriteEntry(archive, "map_metadata.json", """{"Width":10,"Height":10}""");
      var emptyTerrain = string.Join(' ', Enumerable.Repeat("0", 100));
      var emptyWater = string.Join(' ', Enumerable.Repeat("0:0:0:0", 100));
      var worldJson = """
          {"Entities":[
            {"Components":{"LivingNaturalResource":{},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"LivingNaturalResource":{"IsDead":false},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"LivingNaturalResource":{"IsDead":true},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}},
            {"Components":{"LivingNaturalResource":{},"Yielder:Gatherable":{"Yield":{"Good":"Berries"}}}},
            {"Components":{"LivingNaturalResource":{},"Yielder:Cuttable":{"Yield":{"Good":"Stone"}}}}
          ],"Singletons":{"TerrainMap":{"Heights":{"Array":"__TERRAIN__"}},
          "WaterMapNew":{"Levels":1,"WaterColumns":{"Array":"__WATER__"}}}}
          """.Replace("__TERRAIN__", emptyTerrain).Replace("__WATER__", emptyWater);
      WriteEntry(archive, "world.json", worldJson);
    }
    archiveStream.Position = 0;

    using var archiveToRead = new ZipArchive(archiveStream, ZipArchiveMode.Read);
    var analysis = MapArchiveAnalyzer.Analyze(archiveToRead);
    var forest = analysis.Classifications[ForestDensityClassifier.FeatureKey];

    Assert.Equal(10, analysis.Width);
    Assert.Equal(10, analysis.Height);
    Assert.Equal(3L, forest.GetProperty("live_tree_count").GetInt64());
    Assert.Equal(0.03, forest.GetProperty("coverage_ratio").GetDouble());
    Assert.Equal(0, forest.GetProperty("level").GetInt32());
    var water = analysis.Classifications[WaterFormClassifier.FeatureKey];
    Assert.Equal("none", water.GetProperty("water_form").GetString());
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

  static void ExcludesOpenWaterFromForestCoverage() {
    using var entity = System.Text.Json.JsonDocument.Parse("""
        {"Components":{"LivingNaturalResource":{},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}}
        """);
    var classifier = new ForestDensityClassifier();
    for (var index = 0; index < 10; index++) {
      classifier.ObserveEntity(entity.RootElement);
    }
    var result = classifier.BuildResult(new MapDimensions(10, 10), 50);
    Assert.Equal(10L, result.GetProperty("live_tree_count").GetInt64());
    Assert.Equal(0.20, result.GetProperty("coverage_ratio").GetDouble());
    Assert.Equal(2, result.GetProperty("level").GetInt32());
  }

  static void TrustsRuntimeMapSize() {
    using var archiveStream = new MemoryStream();
    using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true)) {
      WriteEntry(archive, "map_metadata.json", """{"Width":2,"Height":2}""");
      var terrain = string.Join(' ', Enumerable.Repeat("0", 6));
      var water = string.Join(' ', Enumerable.Repeat("0", 6));
      var worldJson = """
          {"Entities":[],"Singletons":{"MapSize":{"Size":{"X":3,"Y":2}},
          "TerrainMap":{"Heights":{"Array":"__TERRAIN__"}},
          "WaterMapNew":{"Levels":1,"WaterColumns":{"Array":"__WATER__"}}}}
          """.Replace("__TERRAIN__", terrain).Replace("__WATER__", water);
      WriteEntry(archive, "world.json", worldJson);
    }
    archiveStream.Position = 0;

    using var archiveToRead = new ZipArchive(archiveStream, ZipArchiveMode.Read);
    var analysis = MapArchiveAnalyzer.Analyze(archiveToRead);

    Assert.Equal(3, analysis.Width);
    Assert.Equal(2, analysis.Height);
  }

  static void EmitsConcreteWaterForms() {
    Assert.Equal("none", WaterFormClassifier.GetWaterForm(0, 0, 0, 0));
    Assert.Equal("rivers", WaterFormClassifier.GetWaterForm(100, 0, 0, 100));
    Assert.Equal("rivers", WaterFormClassifier.GetWaterForm(100, 1, 10, 90));
    Assert.Equal("lakes", WaterFormClassifier.GetWaterForm(100, 1, 80, 10));
    Assert.Equal("rivers_and_lakes", WaterFormClassifier.GetWaterForm(100, 2, 45, 55));
  }

  static void DecodesLegacySurfaceWater() {
    using var world = System.Text.Json.JsonDocument.Parse("""
        {"Singletons":{"TerrainMap":{"Heights":{"Array":"2 1"}},"WaterMapNew":{
          "Levels":2,"WaterColumns":{"Array":"3:0:0:0 2:0:0:1 1:0:0:2 4:0:0:3"}}}}
        """);
    var water = WaterMapDecoder.Decode(world.RootElement, 2, 1);
    Assert.Equal(2, water.OpenWaterTileCount);
    Assert.Equal(1, water.UndergroundWaterColumnCount);
    Assert.Equal(1f, water.SurfaceDepths[0]);
    Assert.Equal(2f, water.SurfaceDepths[1]);
  }

  static void DecodesVoxelSurfaceWater() {
    using var world = System.Text.Json.JsonDocument.Parse("""
        {"Singletons":{"TerrainMap":{"Voxels":{"Array":"1 1 1 0 0 0"}},"WaterMapNew":{
          "Levels":1,"WaterColumns":{"Array":"2.5:0:0:2 1.5:0:0:1"}}}}
        """);
    var water = WaterMapDecoder.Decode(world.RootElement, 2, 1);
    Assert.Equal(2, water.OpenWaterTileCount);
    Assert.Equal(2.5f, water.SurfaceDepths[0]);
    Assert.Equal(1.5f, water.SurfaceDepths[1]);
  }

  static void DetectsIrregularLakeCore() {
    var depths = Enumerable.Repeat(3f, 81).ToArray();
    foreach (var dryCell in new[] { 0, 1, 7, 8, 9, 17, 63, 71, 72, 73, 79, 80 }) {
      depths[dryCell] = 0;
    }
    var map = new DecodedWaterMap(
        9, 9, new int[81], new int[81], depths, new float[81], new float[81], [], 0, 1);
    var features = WaterFeatureDiagnostics.Analyze(map);
    Assert.Equal(1, features.LakeCount);
    Assert.Equal(25, features.LakeCoreTileCount);
  }

  static void ExcludesLakeShoreFromRiverCandidates() {
    var depths = new float[12 * 12];
    for (var y = 2; y <= 8; y++) {
      for (var x = 2; x <= 8; x++) {
        depths[x + y * 12] = x is 2 or 8 || y is 2 or 8 ? 1 : 3;
      }
    }
    for (var x = 0; x < 12; x++) {
      depths[x + 11 * 12] = 1;
    }
    var map = new DecodedWaterMap(
        12, 12, new int[144], new int[144], depths, new float[144], new float[144], [], 0, 1);
    var features = WaterFeatureDiagnostics.Analyze(map);
    Assert.Equal(40, features.LakeShoreMask.Count(value => value));
    Assert.Equal(12, features.RiverCandidateTileCount);
  }

  static void RequiresTwoDimensionalShallowLakeCore() {
    var depths = new float[20 * 20];
    for (var y = 2; y <= 8; y++) {
      for (var x = 2; x <= 8; x++) {
        depths[x + y * 20] = 1;
      }
    }
    for (var y = 12; y <= 16; y++) {
      for (var x = 1; x <= 18; x++) {
        depths[x + y * 20] = 1;
      }
    }
    var map = new DecodedWaterMap(
        20, 20, new int[400], new int[400], depths, new float[400], new float[400], [], 0, 1);
    var features = WaterFeatureDiagnostics.Analyze(map);
    Assert.Equal(1, features.ShallowLakeCount);
    Assert.Equal(9, features.ShallowLakeCoreTileCount);
    Assert.True(features.RiverCandidateTileCount > 0);
  }

  static void UsesExpectedPlateauBands() {
    Assert.Equal("few_plateaus", PlateauClassifier.GetLevel(0.249999, 0.20));
    Assert.Equal("has_plateaus", PlateauClassifier.GetLevel(0.25, 0.20));
    Assert.Equal("has_plateaus", PlateauClassifier.GetLevel(0.449999, 0.20));
    Assert.Equal("many_plateaus", PlateauClassifier.GetLevel(0.45, 0.20));
    Assert.Equal("flat_map", PlateauClassifier.GetLevel(0.80, 0.80));
    Assert.Equal("flat_map", PlateauClassifier.GetLevel(0.85, 0.70));
    Assert.Equal("many_plateaus", PlateauClassifier.GetLevel(0.849999, 0.70));
  }

  static void AcceptsNearbyFlatHeights() {
    var heights = new int[20 * 20];
    for (var y = 0; y < 20; y++) {
      for (var x = 10; x < 20; x++) {
        heights[x + y * 20] = 1;
      }
    }
    var result = PlateauClassifier.Analyze(CreateDryMap(20, 20, heights));
    Assert.Equal("flat_map", result.PlateauLevel);
    Assert.Equal(2, result.PlateauCount);
    Assert.Equal(1d, result.PlateauLandRatio);
  }

  static void KeepsDistinctTerrainLevels() {
    var heights = new int[20 * 20];
    for (var y = 0; y < 20; y++) {
      for (var x = 10; x < 20; x++) {
        heights[x + y * 20] = 4;
      }
    }
    var result = PlateauClassifier.Analyze(CreateDryMap(20, 20, heights));
    Assert.Equal("many_plateaus", result.PlateauLevel);
    Assert.Equal(2, result.PlateauCount);
    Assert.Equal(1d, result.PlateauLandRatio);
  }

  static void ExcludesOpenWaterFromPlateaus() {
    var map = CreateDryMap(20, 20, new int[400]);
    Array.Fill(map.SurfaceDepths, 1f);
    for (var y = 5; y < 15; y++) {
      for (var x = 5; x < 15; x++) {
        map.SurfaceDepths[x + y * 20] = 0;
      }
    }
    var result = PlateauClassifier.Analyze(map);
    Assert.Equal("flat_map", result.PlateauLevel);
    Assert.Equal(1, result.PlateauCount);
    Assert.Equal(1d, result.PlateauLandRatio);
  }

  static DecodedWaterMap CreateDryMap(int width, int height, int[] heights) {
    var area = checked(width * height);
    return new DecodedWaterMap(
        width, height, heights, (int[]) heights.Clone(), new float[area], new float[area], new float[area], [], 0, 1);
  }

  static void WriteEntry(ZipArchive archive, string name, string contents) {
    var entry = archive.CreateEntry(name);
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write(contents);
  }
}

// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text;
using IgorZ.MapBrowser.MapAnalysisFixtureGeneration;
using IgorZ.MapBrowser.WorkshopMapIndexing;
using IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Tests;

static class MapMetadataIndexerTests {
  static readonly MapArchiveAnalyzer ArchiveAnalyzer = new();
  static readonly ForestDensityClassifier ForestClassifier = new();
  static readonly WaterMapDecoder WaterDecoder = new();
  static readonly WaterFeatureDiagnostics WaterDiagnostics = new();
  static readonly WaterFormClassifier WaterClassifier = new();
  static readonly SettlementSpaceClassifier SettlementClassifier = new();
  static readonly IslandClassifier IslandClassifier = new();
  static readonly List<(string Name, Action Test)> Tests = [
      ("Archive analysis counts log trees unless explicitly dead", CountsOnlyLivingLogTrees),
      ("Archive analysis trusts runtime map size over stale metadata", TrustsRuntimeMapSize),
      ("Forest levels use five evenly spaced bands", UsesExpectedForestBands),
      ("Forest coverage excludes open surface water", ExcludesOpenWaterFromForestCoverage),
      ("Forest classifier preserves Workshop map baselines", PreservesForestMapBaselines),
      ("Water form always emits a searchable concrete value", EmitsConcreteWaterForms),
      ("Water decoder reads legacy heights and excludes buried columns", DecodesLegacySurfaceWater),
      ("Water decoder reads voxel terrain", DecodesVoxelSurfaceWater),
      ("Lake diagnostics accept irregular connected shapes", DetectsIrregularLakeCore),
      ("River diagnostics exclude shallow lake shores", ExcludesLakeShoreFromRiverCandidates),
      ("River diagnostics exclude deep lake cores", ExcludesDeepLakeCoreFromRiverCandidates),
      ("Water coverage requires a broad surface behind the wet boundary", ReportsBroadBoundaryWaterRatio),
      ("Water coverage reports dominant connected surfaces", ReportsLargestWaterBodyRatio),
      ("Shallow lake diagnostics require a two-dimensional core", RequiresTwoDimensionalShallowLakeCore),
      ("Lake diagnostics allow a readable basin to cross the map edge", AllowsLakeAcrossMapEdge),
      ("Water classifier preserves reviewed Workshop map baselines", PreservesWaterMapBaselines),
      ("Settlement-space levels use absolute capacity and dominant shape", UsesExpectedSettlementSpaceLevels),
      ("Settlement-space classifier accepts disconnected nearby heights as plain", AcceptsNearbyPlainHeights),
      ("Settlement-space classifier keeps separated terrain levels distinct", KeepsDistinctTerrainLevels),
      ("Settlement-space classifier excludes open water", ExcludesOpenWaterFromSettlementSpace),
      ("Settlement-space classifier preserves reviewed Workshop map baselines", PreservesSettlementSpaceBaselines),
      ("Island classifier preserves reviewed Workshop map baselines", PreservesIslandMapBaselines),
      ("Payload cache keys use Workshop ID and canonical update time", BuildsStablePayloadCacheKey),
      ("Payload cache shards use stable Workshop ID modulo", BuildsStablePayloadCacheShard),
      ("Steam pacing applies the configured normal delay between requests", AppliesNormalSteamRequestDelay),
      ("Steam slow mode requires six consecutive successes", RequiresSixSuccessesToRecoverSteamPacing),
      ("Steam slow mode extends a shorter retry cooldown", ExtendsShortSteamRetryCooldown),
      ("Steam retry cooldown is not extended by slow mode", DoesNotExtendExistingSteamRetryCooldown),
      ("Steam Fail is transient in normal and slow modes", TreatsFailAsTransientInEveryMode),
      ("Unsupported maps are excluded from payload cache population", ExcludesUnsupportedMapsFromCachePopulation),
      ("Stop requests are detected through the configured file", DetectsStopRequestFile),
      ("Steam reconnect threshold counts download requests", AppliesSteamReconnectThreshold),
      ("Steam reconnect resets request pacing", ResetsSteamPacingForReconnect),
  ];

  /// <summary>Runs exact map-analysis, cache, pacing, and checkpoint regression tests.</summary>
  public static int Run() {
    return TestRunner.Run(Tests);
  }

  static void ExcludesUnsupportedMapsFromCachePopulation() {
    Assert.False(MapPayloadCachePolicy.ShouldPopulate("unsupported", false, false));
    Assert.True(MapPayloadCachePolicy.ShouldPopulate("fetched", false, false));
    Assert.False(MapPayloadCachePolicy.ShouldPopulate("fetched", true, false));
    Assert.False(MapPayloadCachePolicy.ShouldPopulate("fetched", false, true));
  }

  static void DetectsStopRequestFile() {
    var path = Path.Combine(Path.GetTempPath(), $"map-index-stop-{Guid.NewGuid():N}");
    Assert.False(StopRequestMonitor.IsStopRequested(null));
    Assert.False(StopRequestMonitor.IsStopRequested(path));
    try {
      File.WriteAllText(path, string.Empty);
      Assert.True(StopRequestMonitor.IsStopRequested(path));
    } finally {
      File.Delete(path);
    }
  }

  static void AppliesSteamReconnectThreshold() {
    Assert.False(SteamReconnectPolicy.ShouldReconnect(249, 250));
    Assert.True(SteamReconnectPolicy.ShouldReconnect(250, 250));
    Assert.True(SteamReconnectPolicy.ShouldReconnect(251, 250));
    Assert.False(SteamReconnectPolicy.ShouldReconnect(500, 0));
  }

  static void ResetsSteamPacingForReconnect() {
    var delays = new List<TimeSpan>();
    var pacer = new SteamRequestPacer(delays.Add, _ => { }, slowModeDelay: TimeSpan.FromSeconds(40));
    pacer.WaitBeforeRequest(TimeSpan.Zero);
    pacer.RecordTransientFailure("k_EResultBusy");

    pacer.ResetForNewSession();
    pacer.WaitBeforeRequest(TimeSpan.Zero);

    Assert.False(pacer.SlowModeActive);
    Assert.Equal(0, pacer.ConsecutiveSuccessfulRequests);
    Assert.Equal(0, delays.Count);
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
    var analysis = ArchiveAnalyzer.Analyze(archiveToRead);
    var forest = analysis.Classifications[ForestDensityClassifier.FeatureKey];

    Assert.Equal(10, analysis.Width);
    Assert.Equal(10, analysis.Height);
    Assert.Equal(3L, forest.GetProperty("live_tree_count").GetInt64());
    Assert.Equal(0.03, forest.GetProperty("coverage_ratio").GetDouble());
    Assert.Equal(0, forest.GetProperty("level").GetInt32());
    var water = analysis.Classifications[WaterFormClassifier.FeatureKey];
    Assert.Equal("none", water.GetProperty("water_form").GetString());
    var islands = analysis.Classifications[IslandClassifier.FeatureKey];
    Assert.Equal(0, islands.GetArrayLength());
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
    const string tree = """{"Components":{"LivingNaturalResource":{},"Yielder:Cuttable":{"Yield":{"Good":"Log"}}}}""";
    using var world = System.Text.Json.JsonDocument.Parse(
        $$"""{"Entities":[{{string.Join(',', Enumerable.Repeat(tree, 10))}}]}""");
    var result = new ForestDensityClassifier().Analyze(world.RootElement, 50);
    Assert.Equal(10L, result.LiveTreeCount);
    Assert.Equal(0.20, result.CoverageRatio);
    Assert.Equal(2, result.Level);
  }

  static void PreservesForestMapBaselines() {
    var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Forest");
    var expected = new Dictionary<string, (long Trees, int Level)>() {
        ["00100-3652824726.json.gz"] = (936, 2),
        ["001-musje-3672607632.json.gz"] = (4156, 1),
        ["112-3742639403.json.gz"] = (1031, 1),
        ["challenge-small-3775076404.json.gz"] = (292, 2),
        ["creek-3685093589.json.gz"] = (200, 2),
        ["down-by-the-river-3275489141.json.gz"] = (784, 2),
        ["gemini-origins-3758706362.json.gz"] = (167, 4),
        ["grand-river-3752545142.json.gz"] = (843, 0),
        ["hurmevesi-3760651666.json.gz"] = (193, 0),
        ["limited-3761906496.json.gz"] = (196, 1),
        ["mountain-pool-3721128633.json.gz"] = (304, 0),
        ["painting-wall-3350796155.json.gz"] = (0, 0),
        ["ponds-3759577966.json.gz"] = (449, 0),
        ["shallow-falls-25x25-3755358505.json.gz"] = (224, 3),
        ["spaceship-3744715163.json.gz"] = (26, 3),
        ["the-lake-3769190684.json.gz"] = (1470, 0),
        ["tiny-plateaus-3725408732.json.gz"] = (66, 4),
    };
    foreach (var (fixtureName, baseline) in expected) {
      var (world, landArea) = ForestRegressionFixture.Read(Path.Combine(fixtures, fixtureName));
      var result = ForestClassifier.Analyze(world, landArea);
      Assert.Equal(baseline.Trees, result.LiveTreeCount);
      Assert.Equal(baseline.Trees / (double) landArea, result.CoverageRatio);
      Assert.Equal(baseline.Level, result.Level);
    }
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
    var analysis = ArchiveAnalyzer.Analyze(archiveToRead);

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
    var water = WaterDecoder.Decode(world.RootElement, 2, 1);
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
    var water = WaterDecoder.Decode(world.RootElement, 2, 1);
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
    var features = WaterDiagnostics.Analyze(map);
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
    var features = WaterDiagnostics.Analyze(map);
    Assert.Equal(40, features.LakeShoreMask.Count(value => value));
    Assert.Equal(12, features.RiverCandidateTileCount);
  }

  static void ExcludesDeepLakeCoreFromRiverCandidates() {
    var depths = new float[12 * 12];
    for (var y = 2; y <= 8; y++) {
      for (var x = 2; x <= 8; x++) {
        depths[x + y * 12] = x is 2 or 8 || y is 2 or 8 ? 1 : 3;
      }
    }
    var map = new DecodedWaterMap(
        12, 12, new int[144], new int[144], depths, new float[144], new float[144], [], 0, 1);
    var features = WaterDiagnostics.Analyze(map);
    Assert.Equal(1, features.LakeCount);
    Assert.Equal(0, features.RiverCandidateTileCount);
  }

  static void ReportsBroadBoundaryWaterRatio() {
    var depths = new float[10 * 10];
    for (var y = 0; y < 5; y++) {
      for (var x = 0; x < 10; x++) {
        depths[x + y * 10] = 1;
      }
    }
    var map = new DecodedWaterMap(
        10, 10, new int[100], new int[100], depths, new float[100], new float[100], [], 0, 1);
    Assert.Equal(0.50, WaterFormClassifier.GetBroadBoundaryWaterRatio(map));

    Array.Fill(depths, 0);
    for (var x = 0; x < 10; x++) {
      depths[x] = 1;
    }
    Assert.True(WaterFormClassifier.GetBroadBoundaryWaterRatio(map) < 0.50);
  }

  static void ReportsLargestWaterBodyRatio() {
    var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Water");
    var badTideOcean = WaterClassifier.Analyze(WaterRegressionFixture.Read(
        Path.Combine(fixtures, "bad-tide-ocean-3749624511.json.gz")));
    var atlantis = WaterClassifier.Analyze(WaterRegressionFixture.Read(
        Path.Combine(fixtures, "atlantis-in-trouble-3359016566.json.gz")));
    var lakes = WaterClassifier.Analyze(WaterRegressionFixture.Read(
        Path.Combine(fixtures, "112-3742639403.json.gz")));

    Assert.True(badTideOcean.LargestWaterBodyRatio >= 0.64);
    Assert.True(badTideOcean.BroadBoundaryWaterRatio < 0.50);
    Assert.True(atlantis.LargestWaterBodyRatio >= 0.45);
    Assert.True(atlantis.BroadBoundaryWaterRatio < 0.02);
    Assert.True(lakes.OpenWaterRatio > 0.45);
    Assert.True(lakes.LargestWaterBodyRatio < 0.31);
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
    var features = WaterDiagnostics.Analyze(map);
    Assert.Equal(1, features.ShallowLakeCount);
    Assert.Equal(9, features.ShallowLakeCoreTileCount);
    Assert.True(features.RiverCandidateTileCount > 0);
  }

  static void PreservesWaterMapBaselines() {
    var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Water");
    AssertWaterFixture(fixtures, "down-by-the-river-3275489141.json.gz", "rivers", 1);
    AssertWaterFixture(fixtures, "painting-wall-3350796155.json.gz", "rivers", 2);
    AssertWaterFixture(fixtures, "00100-3652824726.json.gz", "rivers", 1);
    AssertWaterFixture(fixtures, "001-musje-3672607632.json.gz", "rivers", 0);
    AssertWaterFixture(fixtures, "creek-3685093589.json.gz", "rivers_and_lakes", 1);
    AssertWaterFixture(fixtures, "mountain-pool-3721128633.json.gz", "rivers", 2);
    AssertWaterFixture(fixtures, "tiny-plateaus-3725408732.json.gz", "rivers", 0);
    AssertWaterFixture(fixtures, "grand-river-3752545142.json.gz", "rivers", 3);
    AssertWaterFixture(fixtures, "gemini-origins-3758706362.json.gz", "rivers", 0);
    AssertWaterFixture(fixtures, "ponds-3759577966.json.gz", "rivers_and_lakes", 7);
    AssertWaterFixture(fixtures, "112-3742639403.json.gz", "lakes", 8);
    AssertWaterFixture(fixtures, "hurmevesi-3760651666.json.gz", "lakes", 4);
    AssertWaterFixture(fixtures, "the-lake-3769190684.json.gz", "lakes", 2);
    AssertWaterFixture(fixtures, "challenge-small-3775076404.json.gz", "rivers", 0);
    AssertWaterFixture(fixtures, "limited-3761906496.json.gz", "rivers", 0);
  }

  static void AllowsLakeAcrossMapEdge() {
    const int width = 20;
    const int height = 20;
    var depths = new float[width * height];
    for (var y = 3; y <= 16; y++) {
      for (var x = 0; x <= 10; x++) {
        depths[x + y * width] = 3;
      }
    }
    var map = new DecodedWaterMap(
        width, height, new int[width * height], new int[width * height], depths,
        new float[width * height], new float[width * height], [], 0, 1);
    var features = WaterDiagnostics.Analyze(map);
    Assert.Equal(1, features.LakeCount);
  }

  static void AssertWaterFixture(
      string fixtureDirectory, string fixtureName, string expectedForm, int expectedLakeCount) {
    var map = WaterRegressionFixture.Read(Path.Combine(fixtureDirectory, fixtureName));
    var features = WaterDiagnostics.Analyze(map);
    var classification = WaterClassifier.Classify(map, features);
    Assert.Equal(expectedForm, classification.WaterForm);
    Assert.Equal(expectedLakeCount, classification.LakeCount);
  }

  static void UsesExpectedSettlementSpaceLevels() {
    Assert.Equal("little_space", SettlementSpaceClassifier.GetSpaceType(7, 1, 1, 1, 0, 0));
    Assert.Equal("plain", SettlementSpaceClassifier.GetSpaceType(8, 0.70, 0.80, 0, 0, 1));
    Assert.Equal("much_space", SettlementSpaceClassifier.GetSpaceType(8, 0.699, 1, 0.34, 0.33, 0.33));
    Assert.Equal("plain", SettlementSpaceClassifier.GetSpaceType(8, 0, 0, 0.35, 0.34, 0.31));
    Assert.Equal("terraces", SettlementSpaceClassifier.GetSpaceType(8, 0, 0, 0.30, 0.36, 0.34));
    Assert.Equal("plateau", SettlementSpaceClassifier.GetSpaceType(8, 0, 0, 0.30, 0.34, 0.36));
  }

  static void AcceptsNearbyPlainHeights() {
    var heights = new int[20 * 20];
    for (var y = 0; y < 20; y++) {
      for (var x = 10; x < 20; x++) {
        heights[x + y * 20] = 1;
      }
    }
    var result = SettlementClassifier.Analyze(CreateDryMap(20, 20, heights));
    Assert.Equal("plain", result.SpaceType);
    Assert.True(result.CoreCount >= 8);
  }

  static void KeepsDistinctTerrainLevels() {
    var heights = new int[20 * 20];
    for (var y = 0; y < 20; y++) {
      for (var x = 10; x < 20; x++) {
        heights[x + y * 20] = 4;
      }
    }
    var result = SettlementClassifier.Analyze(CreateDryMap(20, 20, heights));
    Assert.Equal("plateau", result.SpaceType);
    Assert.True(result.CoreCount >= 8);
  }

  static void ExcludesOpenWaterFromSettlementSpace() {
    var map = CreateDryMap(20, 20, new int[400]);
    Array.Fill(map.SurfaceDepths, 1f);
    for (var y = 5; y < 15; y++) {
      for (var x = 5; x < 15; x++) {
        map.SurfaceDepths[x + y * 20] = 0;
      }
    }
    var result = SettlementClassifier.Analyze(map);
    Assert.Equal("plain", result.SpaceType);
    Assert.Equal(8, result.CoreCount);
  }

  static void PreservesSettlementSpaceBaselines() {
    var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SettlementSpace");
    var expected = new Dictionary<string, string>() {
      ["00100-3652824726.json.gz"] = "plain",
      ["001-musje-3672607632.json.gz"] = "terraces",
      ["30x-3742220646.json.gz"] = "plain",
      ["9x255-painting-wall-3350796155.json.gz"] = "much_space",
      ["basilisk-veins-3685652179.json.gz"] = "terraces",
      ["beavcube-3741817984.json.gz"] = "much_space",
      ["beaver-flats-3534679704.json.gz"] = "plain",
      ["colony-3406201768.json.gz"] = "plain",
      ["compression-suggestion-3756799738.json.gz"] = "little_space",
      ["creek-3685093589.json.gz"] = "plain",
      ["down-by-the-river-3275489141.json.gz"] = "much_space",
      ["floods-3746978232.json.gz"] = "terraces",
      ["gemini-origins-solo-3758706362.json.gz"] = "much_space",
      ["grand-river-nice-side-3752545142.json.gz"] = "much_space",
      ["high-rise-oasis-3743339720.json.gz"] = "plain",
      ["hurmevesi-3760651666.json.gz"] = "plateau",
      ["jonnomap-3492963393.json.gz"] = "plain",
      ["klein-3739717350.json.gz"] = "plain",
      ["liso-3432480619.json.gz"] = "plain",
      ["map-3569648191.json.gz"] = "plain",
      ["mini-3745965092.json.gz"] = "much_space",
      ["minimum-viable-prospects-3777072465.json.gz"] = "little_space",
      ["mountain-pool-3721128633.json.gz"] = "much_space",
      ["ponds-3759577966.json.gz"] = "terraces",
      ["rocky-3772930352.json.gz"] = "plateau",
      ["rose-and-thorns-15x30-3737866930.json.gz"] = "plain",
      ["shallow-falls-25x25-3755358505.json.gz"] = "little_space",
      ["smol-map-3749630859.json.gz"] = "much_space",
      ["spaceship-3744715163.json.gz"] = "little_space",
      ["squares-3381213849.json.gz"] = "plain",
      ["tedium-ad-infinitum-3751734928.json.gz"] = "little_space",
      ["the-challenge-of-the-small-3775076404.json.gz"] = "much_space",
      ["the-lake-jezero-3769190684.json.gz"] = "much_space",
      ["the-sinkhole-3776247360.json.gz"] = "terraces",
      ["the-ten-ten-3467052578.json.gz"] = "plain",
      ["timbermutantninjaborners-3776832826.json.gz"] = "much_space",
      ["tiny-plateaus-3725408732.json.gz"] = "little_space",
      ["tiny-richland-wonder-challenge-3767764157.json.gz"] = "plain",
      ["toll-3755525976.json.gz"] = "much_space",
      ["treasure-room-flats-3681097397.json.gz"] = "plain",
      ["water-fall-valley-3738934579.json.gz"] = "much_space",
    };
    foreach (var (fixtureName, expectedType) in expected) {
      var map = WaterRegressionFixture.Read(Path.Combine(fixtures, fixtureName));
      var result = SettlementClassifier.Analyze(map);
      Assert.Equal(expectedType, result.SpaceType);
    }
  }

  static void PreservesIslandMapBaselines() {
    var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Islands");
    var expected = new Dictionary<string, int[]>() {
      ["1-3588134831.json.gz"] = [],
      ["5-islands-3538483995.json.gz"] = [11_122, 6_923, 1_968, 1_518, 637],
      ["forbidden-island-3484077181.json.gz"] = [989, 887, 874, 872, 583, 514, 314, 275],
      ["spectacle-island-3752828444.json.gz"] = [13_681, 1_010, 564],
      ["the-archipelago-3673227389.json.gz"] = [5_250],
      ["the-water-maze-3719678554.json.gz"] = [634],
    };
    foreach (var (fixtureName, expectedAreas) in expected) {
      var map = WaterRegressionFixture.Read(Path.Combine(fixtures, fixtureName));
      var actualAreas = IslandClassifier.Analyze(map);
      Assert.Equal(string.Join(",", expectedAreas), string.Join(",", actualAreas));
    }
  }

  static void BuildsStablePayloadCacheKey() {
    var utc = OciPayloadCache.CreateEntryName("12345", "2026-08-05T12:34:56Z");
    var offset = OciPayloadCache.CreateEntryName("12345", "2026-08-05T05:34:56-07:00");
    Assert.Equal(utc, offset);
    Assert.Equal("12345/1785933296.timber", utc);
  }

  static void BuildsStablePayloadCacheShard() {
    Assert.Equal("shard-000", OciPayloadCache.CreateShardTag("3675000000"));
    Assert.Equal("shard-032", OciPayloadCache.CreateShardTag("3675000032"));
    Assert.Equal("shard-099", OciPayloadCache.CreateShardTag("99"));
  }

  static void RequiresSixSuccessesToRecoverSteamPacing() {
    var delays = new List<TimeSpan>();
    var pacer = new SteamRequestPacer(delays.Add, _ => { }, slowModeDelay: TimeSpan.FromSeconds(40));
    pacer.RecordTransientFailure("Busy");
    for (var index = 0; index < 5; index++) {
      pacer.WaitBeforeRequest(TimeSpan.Zero);
      pacer.RecordSuccessfulRequest();
    }
    Assert.True(pacer.SlowModeActive);
    Assert.Equal(5, pacer.ConsecutiveSuccessfulRequests);

    pacer.RecordTransientFailure("Timeout");
    Assert.Equal(0, pacer.ConsecutiveSuccessfulRequests);
    for (var index = 0; index < 6; index++) {
      pacer.WaitBeforeRequest(TimeSpan.Zero);
      pacer.RecordSuccessfulRequest();
    }
    Assert.False(pacer.SlowModeActive);
    Assert.Equal(11, delays.Count);
    Assert.True(delays.All(value => value == TimeSpan.FromSeconds(40)));
  }

  static void AppliesNormalSteamRequestDelay() {
    var delays = new List<TimeSpan>();
    var pacer = new SteamRequestPacer(delays.Add, _ => { }, normalModeDelay: TimeSpan.FromSeconds(3));

    pacer.WaitBeforeRequest(TimeSpan.Zero);
    pacer.WaitBeforeRequest(TimeSpan.Zero);
    pacer.WaitBeforeRequest(TimeSpan.FromSeconds(2));
    pacer.WaitBeforeRequest(TimeSpan.FromSeconds(20));

    Assert.Equal(2, delays.Count);
    Assert.Equal(TimeSpan.FromSeconds(3), delays[0]);
    Assert.Equal(TimeSpan.FromSeconds(1), delays[1]);
  }

  static void DoesNotExtendExistingSteamRetryCooldown() {
    var delays = new List<TimeSpan>();
    var pacer = new SteamRequestPacer(delays.Add, _ => { }, slowModeDelay: TimeSpan.FromSeconds(40));
    pacer.RecordTransientFailure("Busy");

    pacer.WaitBeforeRequest(TimeSpan.FromSeconds(40));

    Assert.Equal(0, delays.Count);
    Assert.True(pacer.SlowModeActive);
  }

  static void ExtendsShortSteamRetryCooldown() {
    var delays = new List<TimeSpan>();
    var pacer = new SteamRequestPacer(delays.Add, _ => { }, slowModeDelay: TimeSpan.FromSeconds(40));
    pacer.RecordTransientFailure("Busy");

    pacer.WaitBeforeRequest(TimeSpan.FromSeconds(20));

    Assert.Equal(1, delays.Count);
    Assert.Equal(TimeSpan.FromSeconds(20), delays[0]);
    Assert.True(pacer.SlowModeActive);
  }

  static void TreatsFailAsTransientInEveryMode() {
    var pacer = new SteamRequestPacer(_ => { }, _ => { });
    Assert.True(pacer.ShouldTreatAsTransient("k_EResultFail"));

    pacer.RecordTransientFailure("k_EResultNoConnection");
    Assert.True(pacer.ShouldTreatAsTransient("k_EResultFail"));
    Assert.False(pacer.ShouldTreatAsTransient("k_EResultAccessDenied"));

    for (var request = 0; request < 6; request++) {
      pacer.RecordSuccessfulRequest();
    }
    Assert.True(pacer.ShouldTreatAsTransient("k_EResultFail"));
  }

  static DecodedWaterMap CreateDryMap(int width, int height, int[] heights) {
    var area = checked(width * height);
    return new DecodedWaterMap(
        width, height, heights, (int[])heights.Clone(), new float[area], new float[area], new float[area], [], 0, 1);
  }

  static void WriteEntry(ZipArchive archive, string name, string contents) {
    var entry = archive.CreateEntry(name);
    using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
    writer.Write(contents);
  }
}

using System.IO.Compression;
using System.Text.Json;

if (args is ["--write-fixture", var fixtureMap, var fixturePath, var workshopId]) {
  using var fixtureArchive = ZipFile.OpenRead(Path.GetFullPath(fixtureMap));
  using var fixtureMetadata = ReadJson(fixtureArchive, "map_metadata.json");
  using var fixtureWorld = ReadJson(fixtureArchive, "world.json");
  var fixtureWidth = fixtureMetadata.RootElement.GetProperty("Width").GetInt32();
  var fixtureHeight = fixtureMetadata.RootElement.GetProperty("Height").GetInt32();
  WaterRegressionFixture.Write(
      fixturePath, workshopId, WaterMapDecoder.Decode(fixtureWorld.RootElement, fixtureWidth, fixtureHeight));
  Console.WriteLine($"Wrote {Path.GetFullPath(fixturePath)}");
  return 0;
}

if (args.Length != 2) {
  Console.Error.WriteLine(
      "Usage: TimberbornMapWaterDecoder MAP.timber OUTPUT_DIRECTORY\n"
          + "   or: TimberbornMapWaterDecoder --write-fixture MAP.timber OUTPUT.json.gz WORKSHOP_ID");
  return 2;
}

var mapPath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(outputDirectory);
using var archive = ZipFile.OpenRead(mapPath);
var metadata = ReadJson(archive, "map_metadata.json");
var world = ReadJson(archive, "world.json");
var width = metadata.RootElement.GetProperty("Width").GetInt32();
var height = metadata.RootElement.GetProperty("Height").GetInt32();
var water = WaterMapDecoder.Decode(world.RootElement, width, height);
var features = WaterFeatureDiagnostics.Analyze(water);
var classification = WaterFormClassifier.Classify(water, features);

var stem = Path.GetFileNameWithoutExtension(mapPath).Trim();
var summaryPath = Path.Combine(outputDirectory, stem + ".water.json");
var imagePath = Path.Combine(outputDirectory, stem + ".water.bmp");
var featuresPath = Path.Combine(outputDirectory, stem + ".water-features.bmp");
File.WriteAllText(summaryPath, JsonSerializer.Serialize(new {
  source = mapPath,
  width,
  height,
  serialized_water_levels = water.SerializedLevels,
  open_water_tiles = water.OpenWaterTileCount,
  open_water_ratio = water.OpenWaterRatio,
  water_form = classification.WaterForm,
  maximum_surface_depth = water.MaximumSurfaceDepth,
  maximum_surface_flow = water.MaximumSurfaceFlow,
  flowing_surface_tiles = water.SurfaceFlowMagnitudes.Count(flow => flow > 0.0001f),
  surface_flow_quantiles = GetPositiveFlowQuantiles(water.SurfaceFlowMagnitudes),
  shallow_flow_coherence_quantiles = GetQuantiles(SelectValues(water.SurfaceFlowCoherences, features.ShallowWaterMask)),
  lake_flow_coherence_quantiles = GetQuantiles(SelectValues(water.SurfaceFlowCoherences, features.LakeCoreMask)),
  underground_water_columns_excluded = water.UndergroundWaterColumnCount,
  lake_count = features.LakeCount,
  shallow_lake_count = features.ShallowLakeCount,
  lake_core_tiles = features.LakeCoreTileCount,
  shallow_lake_core_tiles = features.ShallowLakeCoreTileCount,
  ambiguous_broad_water_tiles = features.AmbiguousBroadWaterTileCount,
  shallow_water_tiles = features.ShallowWaterTileCount,
  river_candidate_tiles = features.RiverCandidateTileCount,
  broad_region_hydrology = features.BroadRegionHydrology,
}, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
WriteBitmap(imagePath, water);
WriteFeatureBitmap(featuresPath, water, features);
Console.WriteLine($"{stem}: {width}x{height}, open water {water.OpenWaterTileCount} tiles "
    + $"({water.OpenWaterRatio:P2}), max depth {water.MaximumSurfaceDepth:F2}, "
    + $"excluded underground columns {water.UndergroundWaterColumnCount}.");
Console.WriteLine($"Wrote {summaryPath}");
Console.WriteLine($"Wrote {imagePath}");
Console.WriteLine($"Wrote {featuresPath}");
return 0;

static JsonDocument ReadJson(ZipArchive archive, string name) {
  var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"Map archive has no {name} entry.");
  using var stream = entry.Open();
  return JsonDocument.Parse(stream);
}

static object GetPositiveFlowQuantiles(float[] flows) {
  return GetQuantiles(flows.Where(flow => flow > 0));
}

static IEnumerable<float> SelectValues(float[] values, bool[] mask) {
  return values.Where((_, index) => mask[index]);
}

static object GetQuantiles(IEnumerable<float> values) {
  var ordered = values.Order().ToArray();
  float Quantile(double fraction) => ordered.Length == 0
      ? 0
      : ordered[(int) Math.Round((ordered.Length - 1) * fraction)];
  return new { p25 = Quantile(0.25), p50 = Quantile(0.50), p75 = Quantile(0.75), p90 = Quantile(0.90) };
}

static void WriteBitmap(string path, DecodedWaterMap map) {
  const int scale = 3;
  var width = map.Width * scale;
  var height = map.Height * scale;
  var rowBytes = (width * 3 + 3) & ~3;
  var pixelBytes = checked(rowBytes * height);
  using var stream = new BinaryWriter(File.Create(path));
  stream.Write((byte) 'B');
  stream.Write((byte) 'M');
  stream.Write(54 + pixelBytes);
  stream.Write(0);
  stream.Write(54);
  stream.Write(40);
  stream.Write(width);
  stream.Write(height);
  stream.Write((short) 1);
  stream.Write((short) 24);
  stream.Write(0);
  stream.Write(pixelBytes);
  stream.Write(2835);
  stream.Write(2835);
  stream.Write(0);
  stream.Write(0);

  var padding = new byte[rowBytes - width * 3];
  for (var pixelY = 0; pixelY < height; pixelY++) {
    var y = pixelY / scale;
    for (var pixelX = 0; pixelX < width; pixelX++) {
      var x = pixelX / scale;
      var cell = x + y * map.Width;
      var depth = map.SurfaceDepths[cell];
      if (depth > 0) {
        var intensity = (byte) Math.Clamp(120 + depth * 35, 120, 255);
        stream.Write(intensity);
        stream.Write((byte) Math.Clamp(70 + depth * 12, 70, 180));
        stream.Write((byte) 20);
      } else {
        var intensity = (byte) Math.Clamp(45 + map.TerrainHeights[cell] * 6, 45, 180);
        stream.Write((byte) (intensity / 2));
        stream.Write(intensity);
        stream.Write((byte) (intensity / 2));
      }
    }
    stream.Write(padding);
  }
}

static void WriteFeatureBitmap(string path, DecodedWaterMap map, WaterFeatureAnalysis features) {
  WriteColoredBitmap(path, map, (cell, depth, terrainHeight) => {
    if (features.LakeCoreMask[cell]) {
      return (20, 210, 255);
    }
    if (features.ShallowLakeCoreMask[cell]) {
      return (20, 140, 255);
    }
    if (features.AmbiguousBroadWaterMask[cell]) {
      return (230, 230, 230);
    }
    if (features.RiverCandidateMask[cell]) {
      return (255, 220, 20);
    }
    if (features.LakeShoreMask[cell]) {
      return (200, 120, 20);
    }
    if (features.ShallowWaterMask[cell]) {
      return (180, 180, 90);
    }
    if (depth > 0) {
      return (210, 30, 210);
    }
    var intensity = (byte) Math.Clamp(45 + terrainHeight * 6, 45, 180);
    return ((byte) (intensity / 2), intensity, (byte) (intensity / 2));
  });
}

static void WriteColoredBitmap(
    string path, DecodedWaterMap map, Func<int, float, int, (byte Blue, byte Green, byte Red)> getColor) {
  const int scale = 3;
  var width = map.Width * scale;
  var height = map.Height * scale;
  var rowBytes = (width * 3 + 3) & ~3;
  var pixelBytes = checked(rowBytes * height);
  using var stream = new BinaryWriter(File.Create(path));
  stream.Write((byte) 'B');
  stream.Write((byte) 'M');
  stream.Write(54 + pixelBytes);
  stream.Write(0);
  stream.Write(54);
  stream.Write(40);
  stream.Write(width);
  stream.Write(height);
  stream.Write((short) 1);
  stream.Write((short) 24);
  stream.Write(0);
  stream.Write(pixelBytes);
  stream.Write(2835);
  stream.Write(2835);
  stream.Write(0);
  stream.Write(0);

  var padding = new byte[rowBytes - width * 3];
  for (var pixelY = 0; pixelY < height; pixelY++) {
    var y = pixelY / scale;
    for (var pixelX = 0; pixelX < width; pixelX++) {
      var x = pixelX / scale;
      var cell = x + y * map.Width;
      var color = getColor(cell, map.SurfaceDepths[cell], map.TerrainHeights[cell]);
      stream.Write(color.Blue);
      stream.Write(color.Green);
      stream.Write(color.Red);
    }
    stream.Write(padding);
  }
}

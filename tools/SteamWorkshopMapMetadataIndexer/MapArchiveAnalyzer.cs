// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text.Json;
using IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed class MapArchiveAnalyzer {
  public const int AnalysisVersion = 15;
  const long MaxWorldJsonBytes = 250_000_000;

  public MapArchiveAnalysis Analyze(ZipArchive archive) {
    using var world = ReadWorld(archive);
    var dimensions = ReadDimensions(world.RootElement, archive);
    var water = new WaterMapDecoder().Decode(world.RootElement, dimensions.Width, dimensions.Height);
    var waterFeatures = new WaterFeatureDiagnostics().Analyze(water);
    var landArea = checked(dimensions.Width * dimensions.Height) - water.OpenWaterTileCount;
    var classifications = new Dictionary<string, JsonElement>() {
        [ForestDensityClassifier.FeatureKey] = JsonSerializer.SerializeToElement(
            new ForestDensityClassifier().Analyze(world.RootElement, landArea)),
        [WaterFormClassifier.FeatureKey] = JsonSerializer.SerializeToElement(
            new WaterFormClassifier().Classify(water, waterFeatures)),
        [SettlementSpaceClassifier.FeatureKey] = JsonSerializer.SerializeToElement(
            new SettlementSpaceClassifier().Analyze(water)),
        [IslandClassifier.FeatureKey] = JsonSerializer.SerializeToElement(
            new IslandClassifier().Analyze(water, waterFeatures)),
        [CanyonClassifier.FeatureKey] = JsonSerializer.SerializeToElement(
            new CanyonClassifier(water).Analyze()),
        [MountainClassifier.FeatureKey] = JsonSerializer.SerializeToElement(
            new MountainClassifier().Analyze(water)),
    };
    return new MapArchiveAnalysis(dimensions.Width, dimensions.Height, classifications);
  }

  static MapDimensions ReadDimensions(JsonElement world, ZipArchive archive) {
    if (world.TryGetProperty("Singletons", out var singletons)
        && singletons.TryGetProperty("MapSize", out var mapSize)
        && mapSize.TryGetProperty("Size", out var size)
        && size.TryGetProperty("X", out var widthElement)
        && size.TryGetProperty("Y", out var heightElement)
        && widthElement.TryGetInt32(out var width)
        && heightElement.TryGetInt32(out var height)
        && width > 0 && height > 0) {
      return new MapDimensions(width, height);
    }

    var entry = archive.GetEntry("map_metadata.json")
        ?? throw new InvalidDataException("Map archive has no map_metadata.json entry.");
    if (entry.Length is < 1 or > 65_536) {
      throw new InvalidDataException($"Unexpected map_metadata.json size: {entry.Length} bytes.");
    }

    using var stream = entry.Open();
    var dimensions = JsonSerializer.Deserialize<MapDimensions>(stream)
        ?? throw new InvalidDataException("Map metadata could not be deserialized.");
    if (dimensions.Width < 1 || dimensions.Height < 1) {
      throw new InvalidDataException($"Invalid map dimensions {dimensions.Width}x{dimensions.Height}.");
    }
    return dimensions;
  }

  static JsonDocument ReadWorld(ZipArchive archive) {
    var entry = archive.GetEntry("world.json")
        ?? throw new InvalidDataException("Map archive has no world.json entry.");
    if (entry.Length is < 2 or > MaxWorldJsonBytes) {
      throw new InvalidDataException($"Unexpected world.json size: {entry.Length} bytes.");
    }

    using var stream = entry.Open();
    return JsonDocument.Parse(stream);
  }
}

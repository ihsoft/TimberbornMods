// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO.Compression;
using System.Text.Json;
using IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;
using IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed class MapArchiveAnalyzer {
  public const int AnalysisVersion = 9;
  const long MaxWorldJsonBytes = 250_000_000;

  static readonly IReadOnlyList<Func<IMapEntityClassifier>> ClassifierFactories = [
      static () => new ForestDensityClassifier(),
  ];

  public MapArchiveAnalysis Analyze(ZipArchive archive) {
    using var world = ReadWorld(archive);
    var dimensions = ReadDimensions(world.RootElement, archive);
    var classifiers = ClassifierFactories.Select(factory => factory()).ToArray();
    ScanEntities(world.RootElement, classifiers);
    var water = new WaterMapDecoder().Decode(world.RootElement, dimensions.Width, dimensions.Height);
    var landArea = checked(dimensions.Width * dimensions.Height) - water.OpenWaterTileCount;
    var classifications = classifiers.ToDictionary(
        classifier => classifier.Key,
        classifier => classifier.BuildResult(dimensions, landArea));
    classifications.Add(WaterFormClassifier.FeatureKey,
        JsonSerializer.SerializeToElement(new WaterFormClassifier().Analyze(water)));
    classifications.Add(SettlementSpaceClassifier.FeatureKey,
        JsonSerializer.SerializeToElement(new SettlementSpaceClassifier().Analyze(water)));
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

  static void ScanEntities(JsonElement world, IReadOnlyCollection<IMapEntityClassifier> classifiers) {
    if (!world.TryGetProperty("Entities", out var entities)
        || entities.ValueKind != JsonValueKind.Array) {
      throw new InvalidDataException("Map world has no Entities array.");
    }
    foreach (var entity in entities.EnumerateArray()) {
      foreach (var classifier in classifiers) {
        classifier.ObserveEntity(entity);
      }
    }
  }
}

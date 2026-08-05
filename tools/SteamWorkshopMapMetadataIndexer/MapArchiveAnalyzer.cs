using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

static class MapArchiveAnalyzer {
  public const int AnalysisVersion = 2;
  const long MaxWorldJsonBytes = 250_000_000;

  static readonly IReadOnlyList<Func<IMapEntityClassifier>> ClassifierFactories = [
      static () => new ForestDensityClassifier(),
  ];

  public static MapArchiveAnalysis Analyze(ZipArchive archive) {
    var dimensions = ReadDimensions(archive);
    using var world = ReadWorld(archive);
    var classifiers = ClassifierFactories.Select(factory => factory()).ToArray();
    ScanEntities(world.RootElement, classifiers);
    var classifications = classifiers.ToDictionary(
        classifier => classifier.Key,
        classifier => classifier.BuildResult(dimensions));
    classifications.Add(
        WaterFormClassifier.FeatureKey,
        JsonSerializer.SerializeToElement(
            WaterFormClassifier.Analyze(world.RootElement, dimensions.Width, dimensions.Height)));
    return new MapArchiveAnalysis(dimensions.Width, dimensions.Height, classifications);
  }

  static MapDimensions ReadDimensions(ZipArchive archive) {
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

interface IMapEntityClassifier {
  string Key { get; }

  void ObserveEntity(JsonElement entity);

  JsonElement BuildResult(MapDimensions dimensions);
}

sealed class ForestDensityClassifier : IMapEntityClassifier {
  public const string FeatureKey = "forest_density";
  long _liveTreeCount;

  public string Key => FeatureKey;

  public void ObserveEntity(JsonElement entity) {
    if (entity.ValueKind != JsonValueKind.Object
        || !entity.TryGetProperty("Components", out var components)
        || components.ValueKind != JsonValueKind.Object
        || !components.TryGetProperty("LivingNaturalResource", out var livingResource)
        || livingResource.ValueKind != JsonValueKind.Object
        || !components.TryGetProperty("Yielder:Cuttable", out var cuttable)
        || cuttable.ValueKind != JsonValueKind.Object
        || !cuttable.TryGetProperty("Yield", out var yield)
        || yield.ValueKind != JsonValueKind.Object
        || !yield.TryGetProperty("Good", out var good)
        || good.ValueKind != JsonValueKind.String
        || good.GetString() != "Log") {
      return;
    }
    if (livingResource.TryGetProperty("IsDead", out var isDead) && isDead.ValueKind == JsonValueKind.True) {
      return;
    }
    _liveTreeCount++;
  }

  public JsonElement BuildResult(MapDimensions dimensions) {
    var mapArea = checked((long) dimensions.Width * dimensions.Height);
    var coverageRatio = (double) _liveTreeCount / mapArea;
    return JsonSerializer.SerializeToElement(
        new ForestDensityResult(_liveTreeCount, coverageRatio, GetLevel(coverageRatio)));
  }

  public static int GetLevel(double coverageRatio) {
    if (coverageRatio < 0.05) {
      return 0;
    }
    if (coverageRatio < 0.20) {
      return 1;
    }
    if (coverageRatio < 0.35) {
      return 2;
    }
    return coverageRatio <= 0.50 ? 3 : 4;
  }
}

sealed record MapDimensions(
    [property: JsonPropertyName("Width")] int Width,
    [property: JsonPropertyName("Height")] int Height);

sealed record MapArchiveAnalysis(
    int Width,
    int Height,
    IReadOnlyDictionary<string, JsonElement> Classifications);

sealed record ForestDensityResult(
    [property: JsonPropertyName("live_tree_count")] long LiveTreeCount,
    [property: JsonPropertyName("coverage_ratio")] double CoverageRatio,
    [property: JsonPropertyName("level")] int Level);

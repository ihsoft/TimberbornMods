// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json;
using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class ForestDensityClassifier : IMapEntityClassifier {
  public const string FeatureKey = "forest_density";

  sealed record ForestDensityResult(
      [property: JsonPropertyName("live_tree_count")] long LiveTreeCount,
      [property: JsonPropertyName("coverage_ratio")] double CoverageRatio,
      [property: JsonPropertyName("level")] int Level);

  long _liveTreeCount;

  public string Key => FeatureKey;

  public void ObserveEntity(JsonElement entity) {
    if (entity.ValueKind != JsonValueKind.Object
        || !entity.TryGetProperty("Components", out var components)
        || components.ValueKind != JsonValueKind.Object
        || !components.TryGetProperty("Yielder:Cuttable", out var cuttable)
        || cuttable.ValueKind != JsonValueKind.Object
        || !cuttable.TryGetProperty("Yield", out var yield)
        || yield.ValueKind != JsonValueKind.Object
        || !yield.TryGetProperty("Good", out var good)
        || good.ValueKind != JsonValueKind.String
        || good.GetString() != "Log") {
      return;
    }
    if (components.TryGetProperty("LivingNaturalResource", out var livingResource)
        && livingResource.ValueKind == JsonValueKind.Object
        && livingResource.TryGetProperty("IsDead", out var isDead)
        && isDead.ValueKind == JsonValueKind.True) {
      return;
    }
    _liveTreeCount++;
  }

  public JsonElement BuildResult(MapDimensions mapDimensions, int landArea) {
    var coverageRatio = landArea > 0 ? (double) _liveTreeCount / landArea : 0;
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

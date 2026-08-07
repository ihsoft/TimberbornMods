// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

sealed class ForestDensityClassifier {
  public const string FeatureKey = "forest_density";

  public ForestDensityClassification Analyze(JsonElement world, int landArea) {
    if (!world.TryGetProperty("Entities", out var entities)
        || entities.ValueKind != JsonValueKind.Array) {
      throw new InvalidDataException("Map world has no Entities array.");
    }
    var liveTreeCount = entities.EnumerateArray().LongCount(IsLiveTree);
    var coverageRatio = landArea > 0 ? (double) liveTreeCount / landArea : 0;
    return new ForestDensityClassification(liveTreeCount, coverageRatio, GetLevel(coverageRatio));
  }

  static bool IsLiveTree(JsonElement entity) {
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
      return false;
    }
    if (components.TryGetProperty("LivingNaturalResource", out var livingResource)
        && livingResource.ValueKind == JsonValueKind.Object
        && livingResource.TryGetProperty("IsDead", out var isDead)
        && isDead.ValueKind == JsonValueKind.True) {
      return false;
    }
    return true;
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

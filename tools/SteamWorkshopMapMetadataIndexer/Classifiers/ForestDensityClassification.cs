// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>Search-facing summary of live tree coverage on dry land.</summary>
sealed record ForestDensityClassification {
  public ForestDensityClassification(long liveTreeCount, double coverageRatio, int level) {
    LiveTreeCount = liveTreeCount;
    CoverageRatio = coverageRatio;
    Level = level;
  }

  /// <summary>Number of living natural resources that yield logs when cut.</summary>
  [JsonPropertyName("live_tree_count")]
  public long LiveTreeCount { get; }

  /// <summary>Live tree count divided by the number of map tiles not covered by open surface water.</summary>
  [JsonPropertyName("coverage_ratio")]
  public double CoverageRatio { get; }

  /// <summary>Stable search level derived from <see cref="CoverageRatio"/>.</summary>
  [JsonPropertyName("level")]
  public int Level { get; }
}

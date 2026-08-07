// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>Search-facing summary of dry, sufficiently wide settlement areas.</summary>
sealed record SettlementSpaceClassification {
  public SettlementSpaceClassification(
      int coreCount, double plainShare, double terraceShare, double plateauShare, double mixedShare, string spaceType) {
    CoreCount = coreCount;
    PlainShare = plainShare;
    TerraceShare = terraceShare;
    PlateauShare = plateauShare;
    MixedShare = mixedShare;
    SpaceType = spaceType;
  }

  /// <summary>
  /// Number of non-overlapping buildable cores found inside qualifying flat regions. Core spacing scales with map size.
  /// </summary>
  [JsonPropertyName("core_count")]
  public int CoreCount { get; }

  /// <summary>Share of all cores located in regions without a significant elevation boundary.</summary>
  [JsonPropertyName("plain_share")]
  public double PlainShare { get; }

  /// <summary>Share of all cores on regions bounded by higher and lower terrain on opposite sides.</summary>
  [JsonPropertyName("terrace_share")]
  public double TerraceShare { get; }

  /// <summary>Share of all cores on elevated regions predominantly bounded by lower terrain.</summary>
  [JsonPropertyName("plateau_share")]
  public double PlateauShare { get; }

  /// <summary>Share of all cores in regions that do not match a single dominant terrain shape.</summary>
  [JsonPropertyName("mixed_share")]
  public double MixedShare { get; }

  /// <summary>Stable search category derived from total capacity and the dominant terrain shape.</summary>
  [JsonPropertyName("space_type")]
  public string SpaceType { get; }
}

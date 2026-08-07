// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>Search-facing summary of open surface water and its dominant form.</summary>
sealed record WaterClassification {
  public WaterClassification(
      int openWaterTiles, double openWaterRatio, double broadBoundaryWaterRatio, int lakeCount, string waterForm) {
    OpenWaterTiles = openWaterTiles;
    OpenWaterRatio = openWaterRatio;
    BroadBoundaryWaterRatio = broadBoundaryWaterRatio;
    LakeCount = lakeCount;
    WaterForm = waterForm;
  }

  /// <summary>Number of map tiles with water above the terrain surface.</summary>
  [JsonPropertyName("open_water_tiles")]
  public int OpenWaterTiles { get; }

  /// <summary>Open-water tile count divided by the full map area.</summary>
  [JsonPropertyName("open_water_ratio")]
  public double OpenWaterRatio { get; }

  /// <summary>
  /// Share of perimeter tiles backed by at least five consecutive inward water tiles. This distinguishes surrounding
  /// water from narrow streams that merely touch the map edge.
  /// </summary>
  [JsonPropertyName("broad_boundary_water_ratio")]
  public double BroadBoundaryWaterRatio { get; }

  /// <summary>Number of recognized deep and shallow lake basins.</summary>
  [JsonPropertyName("lake_count")]
  public int LakeCount { get; }

  /// <summary>Stable search category describing the detected mix of rivers and lakes.</summary>
  [JsonPropertyName("water_form")]
  public string WaterForm { get; }
}

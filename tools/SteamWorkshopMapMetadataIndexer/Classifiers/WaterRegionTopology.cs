// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>Boundary topology of one complete four-connected open-water region.</summary>
sealed record WaterRegionTopology {
  public WaterRegionTopology(
      int waterTiles, bool touchesMapBoundary, int exteriorShoreEdges, int islandShoreEdges) {
    WaterTiles = waterTiles;
    TouchesMapBoundary = touchesMapBoundary;
    ExteriorShoreEdges = exteriorShoreEdges;
    IslandShoreEdges = islandShoreEdges;
  }

  /// <summary>Number of open-water tiles in the complete connected region.</summary>
  public int WaterTiles { get; }

  /// <summary>Whether the water region reaches outside the playable map.</summary>
  public bool TouchesMapBoundary { get; }

  /// <summary>Water-to-land edges whose land belongs to the map's exterior land mass.</summary>
  public int ExteriorShoreEdges { get; }

  /// <summary>Water-to-land edges whose land is enclosed by water.</summary>
  public int IslandShoreEdges { get; }
}

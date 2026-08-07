// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>
/// Intermediate hydrology features used by the water classifier and diagnostic renderer. Every mask is indexed by
/// <c>x + y * mapWidth</c> and has one element per map tile.
/// </summary>
sealed record WaterFeatureAnalysis {
  public WaterFeatureAnalysis(
      bool[] lakeCoreMask, bool[] shallowLakeCoreMask, bool[] ambiguousBroadWaterMask, bool[] shallowWaterMask,
      bool[] lakeShoreMask, bool[] riverCandidateMask, int lakeCount, int shallowLakeCount, int lakeCoreTileCount,
      int shallowLakeCoreTileCount, int ambiguousBroadWaterTileCount, int shallowWaterTileCount,
      int riverCandidateTileCount, IReadOnlyList<BroadWaterHydrology> broadRegionHydrology) {
    LakeCoreMask = lakeCoreMask;
    ShallowLakeCoreMask = shallowLakeCoreMask;
    AmbiguousBroadWaterMask = ambiguousBroadWaterMask;
    ShallowWaterMask = shallowWaterMask;
    LakeShoreMask = lakeShoreMask;
    RiverCandidateMask = riverCandidateMask;
    LakeCount = lakeCount;
    ShallowLakeCount = shallowLakeCount;
    LakeCoreTileCount = lakeCoreTileCount;
    ShallowLakeCoreTileCount = shallowLakeCoreTileCount;
    AmbiguousBroadWaterTileCount = ambiguousBroadWaterTileCount;
    ShallowWaterTileCount = shallowWaterTileCount;
    RiverCandidateTileCount = riverCandidateTileCount;
    BroadRegionHydrology = broadRegionHydrology;
  }

  /// <summary>Broad, level water cells belonging to recognized basins that contain deep water.</summary>
  public bool[] LakeCoreMask { get; }

  /// <summary>Broad, level water cells belonging to recognized entirely shallow basins.</summary>
  public bool[] ShallowLakeCoreMask { get; }

  /// <summary>Broad water that is neither a confident lake nor a confident river.</summary>
  public bool[] AmbiguousBroadWaterMask { get; }

  /// <summary>Open-water cells no deeper than two tiles.</summary>
  public bool[] ShallowWaterMask { get; }

  /// <summary>Shallow open-water cells within two tiles of a recognized lake core.</summary>
  public bool[] LakeShoreMask { get; }

  /// <summary>Remaining connected open water that is large enough to represent a river.</summary>
  public bool[] RiverCandidateMask { get; }

  /// <summary>Number of recognized basins containing water at least three tiles deep.</summary>
  public int LakeCount { get; }

  /// <summary>Number of recognized basins whose water is at most two tiles deep.</summary>
  public int ShallowLakeCount { get; }

  /// <summary>Number of true cells in <see cref="LakeCoreMask"/>.</summary>
  public int LakeCoreTileCount { get; }

  /// <summary>Number of true cells in <see cref="ShallowLakeCoreMask"/>.</summary>
  public int ShallowLakeCoreTileCount { get; }

  /// <summary>Number of true cells in <see cref="AmbiguousBroadWaterMask"/>.</summary>
  public int AmbiguousBroadWaterTileCount { get; }

  /// <summary>Number of true cells in <see cref="ShallowWaterMask"/>.</summary>
  public int ShallowWaterTileCount { get; }

  /// <summary>Number of true cells in <see cref="RiverCandidateMask"/>.</summary>
  public int RiverCandidateTileCount { get; }

  /// <summary>Measurements for each broad, nearly level surface-water component.</summary>
  public IReadOnlyList<BroadWaterHydrology> BroadRegionHydrology { get; }
}

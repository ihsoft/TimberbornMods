// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Decoding;

/// <summary>
/// Tile-aligned terrain, open surface water, and surface-flow data decoded from the serialized game map. Underground
/// water columns are counted for diagnostics but excluded from all tile arrays.
/// </summary>
sealed record DecodedWaterMap {
  public DecodedWaterMap(
      int width, int height, int[] terrainHeights, int[] surfaceFloors, float[] surfaceDepths,
      float[] surfaceFlowMagnitudes, float[] surfaceFlowCoherences, IReadOnlyList<SurfaceFlowEdge> surfaceFlowEdges,
      int undergroundWaterColumnCount, int serializedLevels) {
    Width = width;
    Height = height;
    TerrainHeights = terrainHeights;
    SurfaceFloors = surfaceFloors;
    SurfaceDepths = surfaceDepths;
    SurfaceFlowMagnitudes = surfaceFlowMagnitudes;
    SurfaceFlowCoherences = surfaceFlowCoherences;
    SurfaceFlowEdges = surfaceFlowEdges;
    UndergroundWaterColumnCount = undergroundWaterColumnCount;
    SerializedLevels = serializedLevels;
  }

  /// <summary>Playable map width in tiles.</summary>
  public int Width { get; }

  /// <summary>Playable map height in tiles.</summary>
  public int Height { get; }

  /// <summary>Terrain elevation for each map tile.</summary>
  public int[] TerrainHeights { get; }

  /// <summary>Terrain floor supporting the selected surface-water column for each tile.</summary>
  public int[] SurfaceFloors { get; }

  /// <summary>Open-water depth above terrain for each tile, or zero for dry tiles.</summary>
  public float[] SurfaceDepths { get; }

  /// <summary>Sum of absolute serialized outflows for each surface-water tile.</summary>
  public float[] SurfaceFlowMagnitudes { get; }

  /// <summary>Directional flow alignment from zero for cancelling flow to one for aligned flow.</summary>
  public float[] SurfaceFlowCoherences { get; }

  /// <summary>Positive directed flows between distinct surface-water cells.</summary>
  public IReadOnlyList<SurfaceFlowEdge> SurfaceFlowEdges { get; }

  /// <summary>Non-empty serialized water columns below their tile's terrain height.</summary>
  public int UndergroundWaterColumnCount { get; }

  /// <summary>Number of vertical water levels stored in the map payload.</summary>
  public int SerializedLevels { get; }

  /// <summary>Number of tiles whose decoded surface-water depth is positive.</summary>
  public int OpenWaterTileCount => SurfaceDepths.Count(depth => depth > 0);

  /// <summary>Open surface-water tile count divided by the full map area.</summary>
  public double OpenWaterRatio => (double) OpenWaterTileCount / (Width * Height);

  /// <summary>Greatest decoded surface-water depth, or zero when the map has no tiles.</summary>
  public float MaximumSurfaceDepth => SurfaceDepths.Length == 0 ? 0 : SurfaceDepths.Max();

  /// <summary>Greatest sum of absolute serialized outflows on one surface-water tile.</summary>
  public float MaximumSurfaceFlow => SurfaceFlowMagnitudes.Length == 0 ? 0 : SurfaceFlowMagnitudes.Max();
}

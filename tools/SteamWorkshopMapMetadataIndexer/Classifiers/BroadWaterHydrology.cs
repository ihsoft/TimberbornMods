// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>Shape and flow measurements for one broad, nearly level surface-water component.</summary>
sealed record BroadWaterHydrology {
  public BroadWaterHydrology(
      int coreTiles, int spanX, int spanY, double centerX, double centerY, int boundaryEdges, double compactness,
      int maximumShoreDistance, int innerCoreTiles, double volume, double inflow, double outflow,
      double throughputPerVolume, double medianFlowCoherence, double surfaceHeightSpread,
      WaterRegionTopology topology) {
    CoreTiles = coreTiles;
    SpanX = spanX;
    SpanY = spanY;
    CenterX = centerX;
    CenterY = centerY;
    BoundaryEdges = boundaryEdges;
    Compactness = compactness;
    MaximumShoreDistance = maximumShoreDistance;
    InnerCoreTiles = innerCoreTiles;
    Volume = volume;
    Inflow = inflow;
    Outflow = outflow;
    ThroughputPerVolume = throughputPerVolume;
    MedianFlowCoherence = medianFlowCoherence;
    SurfaceHeightSpread = surfaceHeightSpread;
    Topology = topology;
  }

  /// <summary>Tiles at least three orthogonal steps inside the shoreline.</summary>
  public int CoreTiles { get; }

  /// <summary>Width of the core bounding box in tiles.</summary>
  public int SpanX { get; }

  /// <summary>Height of the core bounding box in tiles.</summary>
  public int SpanY { get; }

  /// <summary>Mean core X coordinate.</summary>
  public double CenterX { get; }

  /// <summary>Mean core Y coordinate.</summary>
  public double CenterY { get; }

  /// <summary>Core-cell edges adjacent to another component or the map boundary.</summary>
  public int BoundaryEdges { get; }

  /// <summary>Core compactness calculated as <c>4 * pi * area / perimeter^2</c>.</summary>
  public double Compactness { get; }

  /// <summary>Greatest orthogonal distance from a core tile to the shoreline.</summary>
  public int MaximumShoreDistance { get; }

  /// <summary>Core tiles at least five orthogonal steps inside the shoreline.</summary>
  public int InnerCoreTiles { get; }

  /// <summary>Sum of surface-water depths across the core.</summary>
  public double Volume { get; }

  /// <summary>Serialized flow entering the core from cells outside it.</summary>
  public double Inflow { get; }

  /// <summary>Serialized flow leaving the core.</summary>
  public double Outflow { get; }

  /// <summary>The smaller of inflow and outflow, divided by core volume.</summary>
  public double ThroughputPerVolume { get; }

  /// <summary>Median directional coherence of serialized flow across core tiles.</summary>
  public double MedianFlowCoherence { get; }

  /// <summary>Difference between the 90th and 10th percentile surface elevations.</summary>
  public double SurfaceHeightSpread { get; }

  /// <summary>Shore topology of the complete connected water region containing this core.</summary>
  public WaterRegionTopology Topology { get; }
}

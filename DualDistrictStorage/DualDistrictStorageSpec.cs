// Timberborn Mod: Dual District Storage
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.DualDistrictStorage;

sealed record DualDistrictStorageSpec : ComponentSpec {
  [Serialize]
  public bool SplitPlaneVisualization { get; init; }

  [Serialize]
  public int VisualizationShareNumerator { get; init; }

  [Serialize]
  public int VisualizationShareDenominator { get; init; }

  [Serialize]
  public bool OwnsSharedPlaneVisualization { get; init; } = true;
}

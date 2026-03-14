// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.TimberCommons.IrrigationSystem;

record GoodConsumingIrrigationTowerSpec : ComponentSpec {
  /// <summary>The maximum distance of irrigation from the building's boundary.</summary>
  [Serialize]
  public int IrrigationRange { get; init; }

  /// <summary>
  /// Indicates that only foundation tiles with "ground only" setting will be considered when searching for the eligible
  /// tiles.
  /// </summary>
  [Serialize]
  public bool IrrigateFromGroundTilesOnly { get; init; } = true;
}
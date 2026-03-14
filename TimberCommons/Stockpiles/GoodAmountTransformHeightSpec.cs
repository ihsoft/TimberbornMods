// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.TimberCommons.Stockpiles;

record GoodAmountTransformHeightSpec : ComponentSpec {
  [Serialize]
  public string TargetName { get; init; }

  [Serialize]
  public float MaxHeight { get; init; }

  [Serialize]
  public string Good { get; init; }

  [Serialize]
  public float NonLinearity { get; init; }
}

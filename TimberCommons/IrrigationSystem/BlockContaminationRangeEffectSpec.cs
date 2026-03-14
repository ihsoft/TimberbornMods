// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.TimberCommons.IrrigationSystem;

record BlockContaminationRangeEffectSpec : ComponentSpec {
  /// <summary>The effect identifier. It is used by the tower logic to identify the right effect.</summary>
  /// <remarks>
  /// Multiple effects can have the same name. In this case, all the matching effects will be considered as a group.
  /// However, if they affect the same property, the effect will not sum up. The best positive and the worst negative
  /// effects will be used.
  /// </remarks>
  [Serialize]
  public string EffectGroup { get; init; }
}

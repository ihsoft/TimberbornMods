// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Immutable;
using Timberborn.BlueprintSystem;

namespace IgorZ.TimberCommons.IrrigationSystem;

record ModifyGrowableGrowthRangeEffectSpec : ComponentSpec {
  /// <summary>The effect identifier. It is used by the tower logic to identify the right effect.</summary>
  /// <remarks>
  /// Multiple effects can have the same name. In this case, all the matching effects will be considered as a group.
  /// However, if they affect the same property, the effect will not sum up. The best positive and the worst negative
  /// effects will be used.
  /// </remarks>
  [Serialize]
  public string EffectGroup { get; init; }

  /// <summary>
  /// The modifier percentage applied to the original tree growth rate. It can increase or decrease the growth rate.
  /// </summary>
  /// <remarks>
  /// Values below <c>0</c> are considered "moderators" and decrease the growth rate. Values above <c>0</c> are
  /// "boosters" and increase the growth rate. The rate modifier is a <i>relative</i> value expressed in percent.
  /// For example, <c>15.5</c> means +15.5%, resulting in a total growth rate of 115.5%. Likewise, a value of
  /// <c>-8.5</c> results in a total growth rate of 91.5%.
  /// </remarks>
  [Serialize]
  public float GrowthRateModifier { get; init; }

  /// <summary>The components that must exist on the growable in order to be a target of this effect.</summary>
  /// <remarks>
  /// <p>If the list is empty, then no restriction by the components is applied.</p>
  /// <p>
  /// The names must be in a full notion, e.g. "<c>Timberborn.Forestry.TreeComponent</c>". The growable will be selected
  /// if <i>any</i> of the components are present on the block object.
  /// </p>
  /// </remarks>
  [Serialize]
  public ImmutableArray<string> ComponentsFilter { get; init; } = [];

  /// <summary>The exact names of the prefabs to be selected for this effect.</summary>
  /// <remarks>If the list is empty, then no restriction by the prefab name is applied.</remarks>
  [Serialize]
  public ImmutableArray<string> PrefabNamesFilter { get; init; } = [];
}

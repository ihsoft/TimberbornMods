// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Immutable;
using Timberborn.BlueprintSystem;

namespace IgorZ.TimberCommons.IrrigationSystem;

record ManufactoryIrrigationTowerSpec : ComponentSpec {
  /// <summary>The maximum distance of irrigation from the building's boundary.</summary>
  [Serialize]
  public int IrrigationRange { get; init; }

  /// <summary>
  /// Indicates that only foundation tiles with "ground only" setting will be considered when searching for the eligible
  /// tiles.
  /// </summary>
  [Serialize]
  public bool IrrigateFromGroundTilesOnly { get; init; } = true;

  /// <summary>Defines rules to apply an effect group per the recipe selected.</summary>
  /// <remarks>Each row is mappings like: <c>&lt;recipe id>=&lt;effect group></c>. The keys must be unique.</remarks>
  /// <seealso cref="ModifyGrowableGrowthRangeEffectSpec"/>
  [Serialize]
  public ImmutableArray<string> Effects { get; init; } = [];
}

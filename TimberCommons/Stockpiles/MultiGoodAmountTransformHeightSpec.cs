// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Immutable;
using Timberborn.BlueprintSystem;

namespace IgorZ.TimberCommons.Stockpiles;

record MultiGoodAmountTransformHeightSpec : ComponentSpec {
  [Serialize]
  public ImmutableArray<GoodAmountTransformHeightSpec> GoodAmounts { get; init; }
}

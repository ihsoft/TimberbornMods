// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Immutable;
using Timberborn.BlueprintSystem;

namespace IgorZ.CustomTools.Core;

/// <summary>Spec to limit the tool visibility.</summary>
/// <remarks>
/// Add it to the blueprint that declares the tool. The blueprint will be loaded and processed by the core game
/// regardless to the limiter settings, but the tool won't be present if the conditions are not satisfied.
/// </remarks>
public record CustomToolFeatureLimiterSpec : ComponentSpec {
  /// <summary>The list of game factions that this tool should be visible to.</summary>
  [Serialize]
  public ImmutableArray<string> AllowedFactions { get; init; }
}

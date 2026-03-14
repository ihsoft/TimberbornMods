// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.GameFactionSystem;

namespace IgorZ.CustomTools.Core;

/// <summary>Controls the custom tools visibility, based on various conditions.</summary>
public sealed class FeatureLimiterService(FactionService factionService) {
  /// <summary>Checks if the tool is allowed.</summary>
  public bool IsAllowed(CustomToolSpec spec) {
    var featureLimiter = spec.GetSpec<CustomToolFeatureLimiterSpec>();
    if (featureLimiter == null) {
      return true;
    }
    if (featureLimiter.AllowedFactions != null && featureLimiter.AllowedFactions.Length > 0
        && !featureLimiter.AllowedFactions.Contains(factionService.Current.Id)) {
      return false;
    }
    return true;
  }
}

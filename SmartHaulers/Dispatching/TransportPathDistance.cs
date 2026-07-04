// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.Navigation;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportPathDistance {
  public static bool TryFindRoadPath(Accessible start, Accessible end, out float distance) {
    distance = float.PositiveInfinity;
    if (AccessibleIsBlocked(start) || AccessibleIsBlocked(end)) {
      return false;
    }
    var pathFound = false;
    foreach (var startAccess in start.Accesses) {
      if (end.FindPathUnlimitedRange(startAccess, [], out var pathDistance)) {
        distance = Math.Min(distance, pathDistance);
        pathFound = true;
      }
    }
    return pathFound;
  }

  static bool AccessibleIsBlocked(Accessible accessible) {
    return !accessible.Enabled || (accessible._blockedAccessible?.IsBlocked() ?? false);
  }
}

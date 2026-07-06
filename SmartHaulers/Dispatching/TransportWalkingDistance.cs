// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.WorkSystem;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportWalkingDistance {
  public static bool TryGetRemainingDistance(Worker worker, out float distance) {
    var walker = worker.GetComponent<Timberborn.WalkingSystem.Walker>();
    if (!walker || walker.Stopped() || walker.PathCorners.Count < 2 || walker.PathFollower == null) {
      distance = 0f;
      return false;
    }

    // This is the same remaining-distance calculation vanilla uses while walking. It avoids rebuilding the path from
    // the worker's current position every diagnostics tick for an already assigned moving worker.
    distance = walker.PathFollower.GetRemainingDistance();
    return true;
  }
}

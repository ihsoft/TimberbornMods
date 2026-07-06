// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportRouteDistanceCacheEntry {
  public bool Found { get; }
  public float Distance { get; }

  public TransportRouteDistanceCacheEntry(bool found, float distance) {
    Found = found;
    Distance = distance;
  }
}

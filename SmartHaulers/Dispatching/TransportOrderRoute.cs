// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.InventorySystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportOrderRoute {
  public Inventory Source { get; }
  public Inventory Target { get; }
  public float RouteDistance { get; }
  public float RemainingDistance { get; }
  public float RemainingTaskHours { get; }
  public float Progress { get; }
  public bool HasKnownEndpoints => Source && Target;

  public TransportOrderRoute(
      Inventory source, Inventory target, float routeDistance, float remainingDistance, float remainingTaskHours,
      float progress) {
    Source = source;
    Target = target;
    RouteDistance = routeDistance;
    RemainingDistance = remainingDistance;
    RemainingTaskHours = remainingTaskHours;
    Progress = progress;
  }
}

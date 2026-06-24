// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.WorkSystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportOrderSnapshot {
  public Guid AgentId { get; }
  public Worker Worker { get; }
  public OrderPhase Phase { get; }
  public Inventory Source { get; }
  public Inventory Target { get; }
  public GoodAmount GoodAmount { get; }
  public float RouteDistance { get; }
  public float RemainingDistance { get; }
  public float Progress { get; }

  public TransportOrderSnapshot(
      Guid agentId, Worker worker, OrderPhase phase, Inventory source, Inventory target, GoodAmount goodAmount,
      float routeDistance, float remainingDistance, float progress) {
    AgentId = agentId;
    Worker = worker;
    Phase = phase;
    Source = source;
    Target = target;
    GoodAmount = goodAmount;
    RouteDistance = routeDistance;
    RemainingDistance = remainingDistance;
    Progress = progress;
  }
}

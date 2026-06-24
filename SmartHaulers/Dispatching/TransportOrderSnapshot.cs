// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.WorkSystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportOrderSnapshot {
  public Guid AgentId { get; }
  public Guid RequesterId { get; }
  public Worker Worker { get; }
  public OrderPhase Phase { get; }
  public BaseComponent Requester { get; }
  public string BehaviorName { get; }
  public float Weight { get; }
  public Inventory Source { get; }
  public Inventory Target { get; }
  public GoodAmount GoodAmount { get; }
  public float RouteDistance { get; }
  public float RemainingDistance { get; }
  public float Progress { get; }

  public TransportOrderSnapshot(
      Guid agentId, Guid requesterId, Worker worker, OrderPhase phase, BaseComponent requester, string behaviorName,
      float weight, Inventory source, Inventory target, GoodAmount goodAmount, float routeDistance,
      float remainingDistance, float progress) {
    AgentId = agentId;
    RequesterId = requesterId;
    Worker = worker;
    Phase = phase;
    Requester = requester;
    BehaviorName = behaviorName;
    Weight = weight;
    Source = source;
    Target = target;
    GoodAmount = goodAmount;
    RouteDistance = routeDistance;
    RemainingDistance = remainingDistance;
    Progress = progress;
  }

  public static TransportOrderSnapshot Assigned(
      Guid agentId, Worker worker, OrderPhase phase, Inventory source, Inventory target, GoodAmount goodAmount,
      float routeDistance, float remainingDistance, float progress) {
    return new TransportOrderSnapshot(
        agentId, Guid.Empty, worker, phase, null, null, float.NaN, source, target, goodAmount, routeDistance,
        remainingDistance, progress);
  }

  public static TransportOrderSnapshot Queued(
      Guid requesterId, BaseComponent requester, string behaviorName, float weight, Inventory source, Inventory target) {
    return new TransportOrderSnapshot(
        Guid.Empty, requesterId, null, OrderPhase.Queued, requester, behaviorName, weight, source, target,
        new GoodAmount(null, 0), float.NaN, float.NaN, float.NaN);
  }
}

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
  public TransportOrderAssignment Assignment { get; }
  public TransportOrderOrigin Origin { get; }
  public TransportOrderRoute Route { get; }
  public TransportCargo Cargo { get; }
  public TransportDecision Decision { get; }
  public OrderPhase Phase { get; }
  public float CriticalTimeInHours { get; }
  public Guid AgentId => Assignment.AgentId;
  public Guid RequesterId => Origin.RequesterId;
  public Worker Worker => Assignment.Worker;
  public BaseComponent Requester => Origin.Requester;
  public string BehaviorName => Origin.BehaviorName;
  public TransportOrderDomain Domain => Origin.Domain;
  public float Weight => Origin.Weight;
  public Inventory Source => Route.Source;
  public Inventory Target => Route.Target;
  public GoodAmount GoodAmount => Cargo.GoodAmount;
  public float RouteDistance => Route.RouteDistance;
  public float RemainingDistance => Route.RemainingDistance;
  public float Progress => Route.Progress;
  public bool HasCriticalTime => !float.IsNaN(CriticalTimeInHours);

  public TransportOrderSnapshot(
      TransportOrderAssignment assignment, TransportOrderOrigin origin, OrderPhase phase, TransportOrderRoute route,
      TransportCargo cargo, TransportDecision decision = default, float criticalTimeInHours = float.NaN) {
    Assignment = assignment;
    Origin = origin;
    Route = route;
    Cargo = cargo;
    Decision = decision;
    Phase = phase;
    CriticalTimeInHours = criticalTimeInHours;
  }

  public TransportOrderSnapshot WithDecision(TransportDecision decision) {
    return new TransportOrderSnapshot(Assignment, Origin, Phase, Route, Cargo, decision, CriticalTimeInHours);
  }

  public TransportOrderSnapshot WithPhase(OrderPhase phase) {
    return new TransportOrderSnapshot(Assignment, Origin, phase, Route, Cargo, Decision, CriticalTimeInHours);
  }

  public TransportOrderSnapshot WithCriticalTime(float criticalTimeInHours) {
    return new TransportOrderSnapshot(Assignment, Origin, Phase, Route, Cargo, Decision, criticalTimeInHours);
  }

  public static TransportOrderSnapshot Assigned(
      Guid agentId, Worker worker, OrderPhase phase, Inventory source, Inventory target, GoodAmount goodAmount,
      float routeDistance, float remainingDistance, float progress) {
    return new TransportOrderSnapshot(
        new TransportOrderAssignment(agentId, worker),
        TransportOrderOrigin.ActiveReservation(),
        phase,
        new TransportOrderRoute(source, target, routeDistance, remainingDistance, progress),
        new TransportCargo(goodAmount));
  }

  public static TransportOrderSnapshot Queued(
      Guid requesterId, BaseComponent requester, string behaviorName, float weight, Inventory source, Inventory target,
      GoodAmount goodAmount) {
    return Queued(
        TransportOrderOrigin.HaulBehavior(requesterId, requester, behaviorName, weight), source, target, goodAmount);
  }

  public static TransportOrderSnapshot Construction(
      Guid requesterId, BaseComponent requester, string behaviorName, float weight, Inventory source, Inventory target,
      GoodAmount goodAmount) {
    return Queued(
        TransportOrderOrigin.ConstructionJob(requesterId, requester, behaviorName, weight), source, target,
        goodAmount);
  }

  static TransportOrderSnapshot Queued(
      TransportOrderOrigin origin, Inventory source, Inventory target, GoodAmount goodAmount) {
    var cargo = new TransportCargo(goodAmount);
    var phase = source && target && cargo.HasGoods ? OrderPhase.Estimated : OrderPhase.Queued;
    return new TransportOrderSnapshot(
        TransportOrderAssignment.None,
        origin,
        phase,
        new TransportOrderRoute(source, target, float.NaN, float.NaN, float.NaN),
        cargo);
  }

  public static TransportOrderSnapshot Covered(
      Guid requesterId, BaseComponent requester, string behaviorName, float weight, Inventory target,
      GoodAmount goodAmount) {
    return new TransportOrderSnapshot(
        TransportOrderAssignment.None,
        TransportOrderOrigin.HaulBehavior(requesterId, requester, behaviorName, weight),
        OrderPhase.Covered,
        new TransportOrderRoute(null, target, float.NaN, float.NaN, float.NaN),
        new TransportCargo(goodAmount));
  }
}

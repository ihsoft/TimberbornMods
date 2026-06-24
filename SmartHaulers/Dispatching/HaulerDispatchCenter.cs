// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.SmartHaulers.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class HaulerDispatchCenter : TickableComponent, IAwakableComponent, IDeletableEntity {
  readonly DispatchCenterRegistry _dispatchCenterRegistry;
  readonly List<TransportAgentSnapshot> _agents = [];
  readonly List<TransportOrderSnapshot> _orders = [];
  readonly Dictionary<Worker, TransportAgentProgress> _progressByAgent = [];

  DistrictCenter _districtCenter;
  System.Guid _districtCenterId;

  public System.Guid DistrictCenterId => _districtCenterId;
  public DistrictCenter DistrictCenter => _districtCenter;
  public IReadOnlyList<TransportAgentSnapshot> Agents => _agents;
  public IReadOnlyList<TransportOrderSnapshot> Orders => _orders;

  public HaulerDispatchCenter(DispatchCenterRegistry dispatchCenterRegistry) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
  }

  public void Awake() {
    _districtCenter = GetComponent<DistrictCenter>();
    _districtCenterId = GetComponent<EntityComponent>()?.EntityId ?? System.Guid.Empty;
    _dispatchCenterRegistry.Register(this);
  }

  public void DeleteEntity() {
    _dispatchCenterRegistry.Unregister(this);
  }

  public override void Tick() {
    if (!SmartHaulersState.DiagnosticsEnabled && !SmartHaulersState.LogSnapshotRequested) {
      return;
    }
    RefreshSnapshots();
  }

  void RefreshSnapshots() {
    _agents.Clear();
    _orders.Clear();
    if (_districtCenter?.DistrictPopulation == null) {
      return;
    }
    foreach (var worker in _districtCenter.DistrictPopulation.GetEnabledCharacters<Worker>()) {
      var agentSnapshot = CreateAgentSnapshot(worker);
      if (!agentSnapshot.IsTransportAgent) {
        continue;
      }
      _agents.Add(agentSnapshot);
      if (TryCreateOrderSnapshot(worker, agentSnapshot, out var orderSnapshot)) {
        _orders.Add(orderSnapshot);
      } else {
        _progressByAgent.Remove(worker);
      }
    }
    _agents.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
    _orders.Sort((left, right) => left.AgentId.CompareTo(right.AgentId));
  }

  TransportAgentSnapshot CreateAgentSnapshot(Worker worker) {
    var goodCarrier = worker.GetComponent<GoodCarrier>();
    var goodReserver = worker.GetComponent<GoodReserver>();
    var navigator = worker.GetComponent<Navigator>();
    var behaviorManager = worker.GetComponent<BehaviorManager>();
    if (!goodCarrier || !goodReserver || !navigator) {
      return TransportAgentSnapshot.NotTransportAgent(worker);
    }
    var worldPosition = navigator.CurrentAccessOrPosition();
    var position = NavigationCoordinateSystem.WorldToGridInt(worldPosition);
    var activity = TransportAgentActivity.Create(goodCarrier, goodReserver, behaviorManager, worker.JobRunning);
    var walkingSpeed = worker.GetComponent<WalkerSpeedManager>()?.GetWalkerSpeedAtCurrentPosition() ?? 0f;
    var entityId = worker.GetComponent<EntityComponent>()?.EntityId ?? System.Guid.Empty;
    return new TransportAgentSnapshot(
        entityId, worker, TransportAgentSnapshot.FormatWorker(worker), position, worldPosition, walkingSpeed,
        goodCarrier.LiftingCapacity,
        activity.State, activity, isTransportAgent: true);
  }

  bool TryCreateOrderSnapshot(
      Worker worker, TransportAgentSnapshot agentSnapshot, out TransportOrderSnapshot orderSnapshot) {
    var goodCarrier = worker.GetComponent<GoodCarrier>();
    var goodReserver = worker.GetComponent<GoodReserver>();
    if (!goodCarrier.IsCarrying && !goodReserver.HasReservedStock && !goodReserver.HasReservedCapacity) {
      orderSnapshot = default;
      return false;
    }
    var source = goodReserver.HasReservedStock ? goodReserver.StockReservation.Inventory : null;
    var target = goodReserver.HasReservedCapacity ? goodReserver.CapacityReservation.Inventory : null;
    var goodAmount = PickGoodAmount(goodCarrier, goodReserver);
    var phase = goodCarrier.IsCarrying ? OrderPhase.Delivering : OrderPhase.PickingUp;
    var routeDistance = TryGetRouteDistance(source, target, out var distance) ? distance : float.NaN;
    var targetInventory = phase == OrderPhase.PickingUp ? source : target;
    var remainingDistance = TryGetRemainingDistance(targetInventory, agentSnapshot.WorldPosition, out var remaining)
        ? remaining
        : float.NaN;
    var progress = TrackProgress(worker, phase, targetInventory, goodAmount, remainingDistance);
    orderSnapshot = new TransportOrderSnapshot(
        agentSnapshot.EntityId, worker, phase, source, target, goodAmount, routeDistance, remainingDistance, progress);
    return true;
  }

  static GoodAmount PickGoodAmount(GoodCarrier goodCarrier, GoodReserver goodReserver) {
    if (goodCarrier.IsCarrying) {
      return goodCarrier.CarriedGood.GoodAmount;
    }
    if (goodReserver.HasReservedStock) {
      return goodReserver.StockReservation.GoodAmount;
    }
    if (goodReserver.HasReservedCapacity) {
      return goodReserver.CapacityReservation.GoodAmount;
    }
    return new GoodAmount(null, 0);
  }

  float TrackProgress(
      Worker worker, OrderPhase phase, Inventory targetInventory, GoodAmount goodAmount, float remainingDistance) {
    if (float.IsNaN(remainingDistance) || !targetInventory) {
      _progressByAgent.Remove(worker);
      return float.NaN;
    }
    if (!_progressByAgent.TryGetValue(worker, out var progress)
        || progress.Phase != phase
        || progress.TargetInventory != targetInventory
        || progress.GoodId != goodAmount.GoodId
        || progress.Amount != goodAmount.Amount) {
      progress = new TransportAgentProgress(phase, targetInventory, goodAmount, remainingDistance);
      _progressByAgent[worker] = progress;
    }
    return progress.ProgressFrom(remainingDistance);
  }

  static bool TryGetRouteDistance(Inventory source, Inventory target, out float distance) {
    var sourceAccessible = source ? source.GetEnabledComponent<Accessible>() : null;
    var targetAccessible = target ? target.GetEnabledComponent<Accessible>() : null;
    if (sourceAccessible && targetAccessible && sourceAccessible.FindRoadPath(targetAccessible, out distance)) {
      return true;
    }
    distance = 0f;
    return false;
  }

  static bool TryGetRemainingDistance(Inventory target, Vector3 position, out float distance) {
    var accessible = target ? target.GetEnabledComponent<Accessible>() : null;
    if (accessible && accessible.FindRoadPath(position, out distance)) {
      return true;
    }
    distance = 0f;
    return false;
  }

}

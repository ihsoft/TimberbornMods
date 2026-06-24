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
using Timberborn.Hauling;
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
  readonly List<WeightedBehavior> _weightedBehaviors = [];
  readonly Dictionary<Worker, TransportAgentProgress> _progressByAgent = [];
  readonly Dictionary<Worker, TransportOrderMemory> _orderMemoryByAgent = [];

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
        _orderMemoryByAgent.Remove(worker);
      }
    }
    AddQueuedOrders();
    _agents.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
    _orders.Sort(CompareOrders);
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
    var behaviorManager = worker.GetComponent<BehaviorManager>();
    if (!goodCarrier.IsCarrying && !goodReserver.HasReservedStock && !goodReserver.HasReservedCapacity) {
      orderSnapshot = default;
      return false;
    }
    if (!IsActiveTransportOrder(behaviorManager, goodCarrier)) {
      orderSnapshot = default;
      return false;
    }
    var source = goodReserver.HasReservedStock ? goodReserver.StockReservation.Inventory : null;
    var target = goodReserver.HasReservedCapacity ? goodReserver.CapacityReservation.Inventory : null;
    var goodAmount = PickGoodAmount(goodCarrier, goodReserver);
    var phase = goodCarrier.IsCarrying ? OrderPhase.Delivering : OrderPhase.PickingUp;
    RestoreKnownEndpoints(worker, goodAmount, ref source, ref target);
    RestoreRequesterEndpoint(behaviorManager, ref source, ref target);
    RestoreDistrictEndpoint(goodAmount, ref source, ref target);
    var routeDistance = TryGetRouteDistance(source, target, out var distance) ? distance : float.NaN;
    var targetInventory = phase == OrderPhase.PickingUp ? source : target;
    var remainingDistance = TryGetRemainingDistance(targetInventory, agentSnapshot.WorldPosition, out var remaining)
        ? remaining
        : float.NaN;
    var progress = TrackProgress(worker, phase, targetInventory, goodAmount, remainingDistance);
    RememberKnownEndpoints(worker, source, target, goodAmount);
    orderSnapshot = TransportOrderSnapshot.Assigned(
        agentSnapshot.EntityId, worker, phase, source, target, goodAmount, routeDistance, remainingDistance, progress);
    return true;
  }

  void RestoreKnownEndpoints(Worker worker, GoodAmount goodAmount, ref Inventory source, ref Inventory target) {
    if (!_orderMemoryByAgent.TryGetValue(worker, out var memory) || !memory.Matches(goodAmount)) {
      return;
    }
    if (!source) {
      source = memory.Source;
    }
    if (!target) {
      target = memory.Target;
    }
  }

  void RememberKnownEndpoints(Worker worker, Inventory source, Inventory target, GoodAmount goodAmount) {
    if (!_orderMemoryByAgent.TryGetValue(worker, out var memory)) {
      memory = new TransportOrderMemory();
      _orderMemoryByAgent[worker] = memory;
    }
    memory.Update(source, target, goodAmount);
  }

  static void RestoreRequesterEndpoint(BehaviorManager behaviorManager, ref Inventory source, ref Inventory target) {
    if (!TryGetRunningWorkplaceBehavior(behaviorManager, out var workplaceBehavior)) {
      return;
    }
    var behaviorName = FormatBehaviorName(workplaceBehavior);
    if (!source && IsTakeAwayBehavior(behaviorName)) {
      source = FindKnownEndpointInventory(workplaceBehavior, behaviorName);
    }
    if (!target && IsBringBehavior(behaviorName)) {
      target = FindKnownEndpointInventory(workplaceBehavior, behaviorName);
    }
  }

  static bool TryGetRunningWorkplaceBehavior(BehaviorManager behaviorManager, out WorkplaceBehavior workplaceBehavior) {
    workplaceBehavior = behaviorManager ? behaviorManager._runningBehavior as WorkplaceBehavior : null;
    return workplaceBehavior;
  }

  static bool IsActiveTransportOrder(BehaviorManager behaviorManager, GoodCarrier goodCarrier) {
    if (goodCarrier.IsCarrying) {
      return true;
    }
    if (!TryGetRunningBehavior(behaviorManager, out var behavior)) {
      return false;
    }
    if (behavior is CarryRootBehavior) {
      return true;
    }
    if (behavior is WorkplaceBehavior workplaceBehavior) {
      return IsTransportWorkplaceBehavior(workplaceBehavior);
    }
    return false;
  }

  static bool TryGetRunningBehavior(BehaviorManager behaviorManager, out Behavior behavior) {
    behavior = behaviorManager ? behaviorManager._runningBehavior : null;
    return behavior;
  }

  static bool IsTransportWorkplaceBehavior(WorkplaceBehavior workplaceBehavior) {
    var behaviorName = FormatBehaviorName(workplaceBehavior);
    return IsBringBehavior(behaviorName) || IsTakeAwayBehavior(behaviorName);
  }

  void RestoreDistrictEndpoint(GoodAmount goodAmount, ref Inventory source, ref Inventory target) {
    var districtInventoryPicker = _districtCenter.GetComponent<DistrictInventoryPicker>();
    if (!districtInventoryPicker) {
      return;
    }
    if (source && !target) {
      var sourceAccessible = source.GetEnabledComponent<Accessible>();
      if (sourceAccessible) {
        target = districtInventoryPicker.ClosestInventoryWithCapacity(sourceAccessible, goodAmount, _ => true, out _);
      }
    }
    if (!source && target) {
      var targetAccessible = target.GetEnabledComponent<Accessible>();
      if (targetAccessible) {
        source = districtInventoryPicker.ClosestInventoryWithStock(targetAccessible, goodAmount.GoodId, _ => true);
      }
    }
  }

  void AddQueuedOrders() {
    var districtBuildingRegistry = _districtCenter.GetComponent<DistrictBuildingRegistry>();
    if (!districtBuildingRegistry) {
      return;
    }
    foreach (var haulCandidate in districtBuildingRegistry.GetEnabledBuildings<HaulCandidate>()) {
      if (!haulCandidate.Enabled) {
        continue;
      }
      haulCandidate.GetWeightedBehaviors(_weightedBehaviors);
      foreach (var weightedBehavior in _weightedBehaviors) {
        if (weightedBehavior.Weight <= 0f) {
          continue;
        }
        var requesterId = haulCandidate.GetComponent<EntityComponent>()?.EntityId ?? System.Guid.Empty;
        var behaviorName = FormatBehaviorName(weightedBehavior.WorkplaceBehavior);
        var source = IsTakeAwayBehavior(behaviorName) ? FindKnownEndpointInventory(haulCandidate, behaviorName) : null;
        var target = IsBringBehavior(behaviorName) ? FindKnownEndpointInventory(haulCandidate, behaviorName) : null;
        _orders.Add(TransportOrderSnapshot.Queued(
            requesterId, haulCandidate, behaviorName, weightedBehavior.Weight, source, target));
      }
      _weightedBehaviors.Clear();
    }
  }

  static int CompareOrders(TransportOrderSnapshot left, TransportOrderSnapshot right) {
    var phaseComparison = left.Phase.CompareTo(right.Phase);
    if (phaseComparison != 0) {
      return phaseComparison;
    }
    if (left.Phase == OrderPhase.Queued) {
      var requesterComparison = left.RequesterId.CompareTo(right.RequesterId);
      if (requesterComparison != 0) {
        return requesterComparison;
      }
      var behaviorComparison = string.CompareOrdinal(left.BehaviorName, right.BehaviorName);
      if (behaviorComparison != 0) {
        return behaviorComparison;
      }
      return right.Weight.CompareTo(left.Weight);
    }
    return left.AgentId.CompareTo(right.AgentId);
  }

  static string FormatBehaviorName(WorkplaceBehavior workplaceBehavior) {
    var name = workplaceBehavior.GetType().Name;
    const string suffix = "WorkplaceBehavior";
    return name.EndsWith(suffix) ? name[..^suffix.Length] : name;
  }

  static bool IsBringBehavior(string behaviorName) {
    return behaviorName is "BringNutrient" or "FillInput" or "ObtainGood";
  }

  static bool IsTakeAwayBehavior(string behaviorName) {
    return behaviorName is "EmptyInventories" or "EmptyOutput" or "RemoveUnwantedStock" or "SupplyGood";
  }

  static Inventory FindKnownEndpointInventory(BaseComponent requester, string behaviorName) {
    var inventories = requester.GetComponent<Inventories>();
    if (inventories) {
      var inventory = PickInventory(inventories, behaviorName);
      if (inventory) {
        return inventory;
      }
    }
    return requester.GetComponent<Inventory>();
  }

  static Inventory PickInventory(Inventories inventories, string behaviorName) {
    foreach (var inventory in inventories.EnabledInventories) {
      if (MatchesBehavior(inventory, behaviorName)) {
        return inventory;
      }
    }
    foreach (var inventory in inventories.EnabledInventories) {
      return inventory;
    }
    return null;
  }

  static bool MatchesBehavior(Inventory inventory, string behaviorName) {
    return behaviorName switch {
        "BringNutrient" or "FillInput" or "ObtainGood" => inventory.IsInput,
        "EmptyOutput" => inventory.IsOutput,
        "EmptyInventories" => inventory.HasAnyUnreservedStock,
        "RemoveUnwantedStock" => inventory.HasUnwantedStock,
        "SupplyGood" => inventory.HasAnyUnreservedStock,
        _ => false,
    };
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

// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using IgorZ.SmartHaulers.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BuilderHubSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.Hauling;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.PrioritySystem;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;
using Timberborn.Workshops;
using Timberborn.WorkSystem;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class HaulerDispatchCenter : TickableComponent, IAwakableComponent, IDeletableEntity {
  readonly DispatchCenterRegistry _dispatchCenterRegistry;
  readonly ConstructionRegistry _constructionRegistry;
  readonly TransportDecisionEvaluator _decisionEvaluator;
  readonly DispatchPerformanceStats _performanceStats;
  readonly List<TransportAgentSnapshot> _agents = [];
  readonly List<TransportOrderSnapshot> _orders = [];
  readonly List<Accessible> _builderHubAccessibles = [];
  readonly List<WeightedBehavior> _weightedBehaviors = [];
  readonly Dictionary<Worker, TransportAgentProgress> _progressByAgent = [];
  readonly Dictionary<Worker, TransportOrderMemory> _orderMemoryByAgent = [];

  DistrictCenter _districtCenter;
  System.Guid _districtCenterId;

  public System.Guid DistrictCenterId => _districtCenterId;
  public DistrictCenter DistrictCenter => _districtCenter;
  public IReadOnlyList<TransportAgentSnapshot> Agents => _agents;
  public IReadOnlyList<TransportOrderSnapshot> Orders => _orders;

  public HaulerDispatchCenter(
      DispatchCenterRegistry dispatchCenterRegistry, ConstructionRegistry constructionRegistry,
      TransportDecisionEvaluator decisionEvaluator, DispatchPerformanceStats performanceStats) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
    _constructionRegistry = constructionRegistry;
    _decisionEvaluator = decisionEvaluator;
    _performanceStats = performanceStats;
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
    RefreshSnapshot();
  }

  public void RefreshSnapshot() {
    _performanceStats.BeginRefresh();
    try {
      RefreshSnapshotInternal();
    } finally {
      _performanceStats.EndRefresh();
    }
  }

  void RefreshSnapshotInternal() {
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
    AddConstructionOrders();
    ClassifyOrderReadiness();
    AddDecisions();
    _agents.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
    _orders.Sort(CompareOrders);
  }

  void ClassifyOrderReadiness() {
    for (var i = 0; i < _orders.Count; i++) {
      _orders[i] = TransportOrderReadinessClassifier.Classify(_orders[i]);
    }
  }

  void AddDecisions() {
    for (var i = 0; i < _orders.Count; i++) {
      var decision = _decisionEvaluator.Evaluate(_orders[i], _agents);
      if (decision.HasWinner) {
        _orders[i] = _orders[i].WithDecision(decision);
      }
    }
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
    var workplaceRole = ClassifyWorkplaceRole(worker);
    var role = ClassifyAgentRole(worker, behaviorManager, workplaceRole);
    return new TransportAgentSnapshot(
        entityId, worker, TransportAgentSnapshot.FormatWorker(worker), position, worldPosition, walkingSpeed,
        goodCarrier.LiftingCapacity, activity.State, role, workplaceRole, activity, isTransportAgent: true);
  }

  static TransportAgentRole ClassifyAgentRole(
      Worker worker, BehaviorManager behaviorManager, TransportWorkplaceRole workplaceRole) {
    if (IsRunningCommunityServiceBehavior(behaviorManager)) {
      return TransportAgentRole.CommunityService;
    }
    if (CanRunCommunityService(worker)) {
      return TransportAgentRole.CommunityService;
    }
    return workplaceRole switch {
        TransportWorkplaceRole.Transport => TransportAgentRole.DedicatedHauler,
        TransportWorkplaceRole.Builder => TransportAgentRole.Builder,
        TransportWorkplaceRole.Production => TransportAgentRole.Production,
        TransportWorkplaceRole.Unknown => TransportAgentRole.Unknown,
        _ => worker.Workplace ? TransportAgentRole.Unknown : TransportAgentRole.Free,
    };
  }

  static bool CanRunCommunityService(Worker worker) {
    if (worker.GetComponent<WorkRefuser>().RefusesWork) {
      return false;
    }
    return !worker.Employed || !worker.GetComponent<WorkerWorkingHours>().AreWorkingHours;
  }

  static TransportWorkplaceRole ClassifyWorkplaceRole(Worker worker) {
    var workplace = worker.Workplace;
    if (!workplace) {
      return TransportWorkplaceRole.None;
    }
    if (workplace.GetComponent<BuilderHubWorkplaceBehavior>()) {
      return TransportWorkplaceRole.Builder;
    }
    if (workplace.GetComponent<HaulingCenter>()) {
      return TransportWorkplaceRole.Transport;
    }
    if (workplace.GetComponent<Manufactory>()) {
      return TransportWorkplaceRole.Production;
    }
    return TransportWorkplaceRole.Unknown;
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
    var source = goodReserver.HasReservedStock ? goodReserver.StockReservation.Inventory : null;
    var target = goodReserver.HasReservedCapacity ? goodReserver.CapacityReservation.Inventory : null;
    if (!IsActiveTransportOrder(behaviorManager, goodCarrier, target)) {
      orderSnapshot = default;
      return false;
    }
    var goodAmount = PickGoodAmount(goodCarrier, goodReserver);
    var phase = goodCarrier.IsCarrying ? OrderPhase.Delivering : OrderPhase.PickingUp;
    RestoreKnownEndpoints(worker, goodAmount, ref source, ref target);
    RestoreRequesterEndpoint(behaviorManager, ref source, ref target);
    RestoreDistrictEndpoint(goodAmount, ref source, ref target);
    var routeDistance = TryGetRouteDistance(source, target, out var distance) ? distance : float.NaN;
    var targetInventory = phase == OrderPhase.PickingUp ? source : target;
    var remainingDistance = TryGetRemainingDistance(phase, targetInventory, agentSnapshot.WorldPosition, out var remaining)
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

  static bool IsActiveTransportOrder(BehaviorManager behaviorManager, GoodCarrier goodCarrier, Inventory target) {
    if (IsSpecializedLooseGoodTarget(target)) {
      return false;
    }
    if (goodCarrier.IsCarrying) {
      return true;
    }
    if (!TryGetRunningBehavior(behaviorManager, out var behavior)) {
      return false;
    }
    if (behavior is CarryRootBehavior) {
      return true;
    }
    if (IsCommunityServiceTransportBehavior(behavior)) {
      return true;
    }
    if (behavior is WorkplaceBehavior workplaceBehavior) {
      return IsTransportWorkplaceBehavior(workplaceBehavior);
    }
    return false;
  }

  static bool IsSpecializedLooseGoodTarget(Inventory target) {
    var workplace = target ? target.GetComponent<Workplace>() : null;
    if (!workplace) {
      return false;
    }
    foreach (var workplaceBehavior in workplace.WorkplaceBehaviors) {
      if (workplaceBehavior.GetType().Name is
          "FarmHouseGoodStackRetrieverWorkplaceBehavior"
          or "GatherWorkplaceBehavior"
          or "LumberjackFlagWorkplaceBehavior") {
        return true;
      }
    }
    return false;
  }

  static bool TryGetRunningBehavior(BehaviorManager behaviorManager, out Behavior behavior) {
    behavior = behaviorManager ? behaviorManager._runningBehavior : null;
    return behavior;
  }

  static bool IsRunningCommunityServiceBehavior(BehaviorManager behaviorManager) {
    return TryGetRunningBehavior(behaviorManager, out var behavior) && IsCommunityServiceTransportBehavior(behavior);
  }

  static bool IsCommunityServiceTransportBehavior(Behavior behavior) {
    return behavior?.GetType().Name == "BringNutrientBehavior";
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
        PossibleTransportOrderPlanner.AddPlans(_districtCenter, haulCandidate, weightedBehavior, _orders);
      }
      _weightedBehaviors.Clear();
    }
  }

  void AddConstructionOrders() {
    var districtBuildingRegistry = _districtCenter.GetComponent<DistrictBuildingRegistry>();
    if (!districtBuildingRegistry) {
      return;
    }
    CollectBuilderHubAccessibles(districtBuildingRegistry);
    if (_builderHubAccessibles.Count == 0) {
      return;
    }
    foreach (var priority in Priorities.Descending) {
      foreach (var constructionJob in _constructionRegistry.GetJobs(priority)) {
        if (ConstructionTransportOrderPlanner.TryPlan(
            _districtCenter, constructionJob, _builderHubAccessibles, priority, out var order)) {
          _orders.Add(order);
        }
      }
    }
    _builderHubAccessibles.Clear();
  }

  void CollectBuilderHubAccessibles(DistrictBuildingRegistry districtBuildingRegistry) {
    _builderHubAccessibles.Clear();
    foreach (var haulCandidate in districtBuildingRegistry.GetEnabledBuildings<HaulCandidate>()) {
      if (!haulCandidate.GetComponent<BuilderHubWorkplaceBehavior>()) {
        continue;
      }
      var accessible = haulCandidate.GetComponent<BuildingAccessible>()?.Accessible;
      if (accessible) {
        _builderHubAccessibles.Add(accessible);
      }
    }
  }

  static int CompareOrders(TransportOrderSnapshot left, TransportOrderSnapshot right) {
    var phaseComparison = left.Phase.CompareTo(right.Phase);
    if (phaseComparison != 0) {
      return phaseComparison;
    }
    if (IsUnassignedOrder(left.Phase)) {
      var requesterComparison = left.RequesterId.CompareTo(right.RequesterId);
      if (requesterComparison != 0) {
        return requesterComparison;
      }
      var behaviorComparison = string.CompareOrdinal(left.BehaviorName, right.BehaviorName);
      if (behaviorComparison != 0) {
        return behaviorComparison;
      }
      var weightComparison = right.Weight.CompareTo(left.Weight);
      if (weightComparison != 0) {
        return weightComparison;
      }
      var goodComparison = string.CompareOrdinal(left.Cargo.GoodId, right.Cargo.GoodId);
      if (goodComparison != 0) {
        return goodComparison;
      }
      var amountComparison = left.Cargo.Amount.CompareTo(right.Cargo.Amount);
      if (amountComparison != 0) {
        return amountComparison;
      }
      var sourceComparison = CompareComponents(left.Source, right.Source);
      return sourceComparison != 0 ? sourceComparison : CompareComponents(left.Target, right.Target);
    }
    return left.AgentId.CompareTo(right.AgentId);
  }

  static int CompareComponents(BaseComponent left, BaseComponent right) {
    return ComponentId(left).CompareTo(ComponentId(right));
  }

  static Guid ComponentId(BaseComponent component) {
    return component ? component.GetComponent<EntityComponent>()?.EntityId ?? Guid.Empty : Guid.Empty;
  }

  static bool IsUnassignedOrder(OrderPhase phase) {
    return phase is OrderPhase.Queued or OrderPhase.Estimated or OrderPhase.Deferred or OrderPhase.Dispatchable
        or OrderPhase.Covered;
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

  bool TryGetRouteDistance(Inventory source, Inventory target, out float distance) {
    _performanceStats.BeginDeliveryPath();
    var start = DispatchPerformanceStats.Timestamp();
    try {
      var sourceAccessible = source ? source.GetEnabledComponent<Accessible>() : null;
      var targetAccessible = target ? target.GetEnabledComponent<Accessible>() : null;
      if (sourceAccessible
          && targetAccessible
          && sourceAccessible.HasSingleAccess
          && sourceAccessible.FindRoadPath(targetAccessible, out distance)) {
        return true;
      }
      distance = 0f;
      return false;
    } finally {
      _performanceStats.EndDeliveryPath(start);
    }
  }

  bool TryGetRemainingDistance(OrderPhase phase, Inventory target, Vector3 position, out float distance) {
    var isPickup = phase == OrderPhase.PickingUp;
    if (isPickup) {
      _performanceStats.BeginPickupPath();
    } else {
      _performanceStats.BeginDeliveryPath();
    }
    var start = DispatchPerformanceStats.Timestamp();
    try {
      var accessible = target ? target.GetEnabledComponent<Accessible>() : null;
      if (accessible && accessible.FindPathUnlimitedRange(position, [], out distance)) {
        return true;
      }
      distance = 0f;
      return false;
    } finally {
      if (isPickup) {
        _performanceStats.EndPickupPath(start);
      } else {
        _performanceStats.EndDeliveryPath(start);
      }
    }
  }

}

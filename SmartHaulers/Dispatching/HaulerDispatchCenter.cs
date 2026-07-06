// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.Buildings;
using Timberborn.BlockingSystem;
using Timberborn.ConstructionSites;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BuilderHubSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.Emptying;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.Hauling;
using Timberborn.InventorySystem;
using Timberborn.InventoryNeedSystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.PrioritySystem;
using Timberborn.TimeSystem;
using Timberborn.WalkingSystem;
using Timberborn.Workshops;
using Timberborn.WorkSystem;
using UnityEngine;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class HaulerDispatchCenter : BaseComponent, IAwakableComponent, IDeletableEntity, ITransportRouteDistanceProvider {
  static readonly HashSet<string> SmartCriticalNeedIds = ["Hunger", "Thirst", "Biofuel", "Power"];
  static readonly HashSet<string> LoggedUnknownWorkplaces = [];
  static readonly HashSet<string> LoggedIgnoredActiveTransports = [];
  static readonly HashSet<string> LoggedManyInventories = [];

  readonly DispatchCenterRegistry _dispatchCenterRegistry;
  readonly ConstructionRegistry _constructionRegistry;
  readonly TransportDecisionEvaluator _decisionEvaluator;
  readonly DispatchPerformanceStats _performanceStats;
  readonly IGoodService _goodService;
  readonly IDayNightCycle _dayNightCycle;
  readonly List<TransportAgentSnapshot> _agents = [];
  readonly List<TransportOrderSnapshot> _orders = [];
  readonly List<Accessible> _builderHubAccessibles = [];
  readonly List<WeightedBehavior> _weightedBehaviors = [];
  readonly Dictionary<Worker, TransportAgentProgress> _progressByAgent = [];
  readonly Dictionary<Worker, TransportOrderMemory> _orderMemoryByAgent = [];
  readonly Dictionary<TransportRouteDistanceCacheKey, TransportRouteDistanceCacheEntry> _routeDistanceCache = [];

  DistrictCenter _districtCenter;
  System.Guid _districtCenterId;

  public System.Guid DistrictCenterId => _districtCenterId;
  public DistrictCenter DistrictCenter => _districtCenter;
  public IReadOnlyList<TransportAgentSnapshot> Agents => _agents;
  public IReadOnlyList<TransportOrderSnapshot> Orders => _orders;

  public HaulerDispatchCenter(
      DispatchCenterRegistry dispatchCenterRegistry, ConstructionRegistry constructionRegistry,
      TransportDecisionEvaluator decisionEvaluator, DispatchPerformanceStats performanceStats,
      IGoodService goodService, IDayNightCycle dayNightCycle) {
    _dispatchCenterRegistry = dispatchCenterRegistry;
    _constructionRegistry = constructionRegistry;
    _decisionEvaluator = decisionEvaluator;
    _performanceStats = performanceStats;
    _goodService = goodService;
    _dayNightCycle = dayNightCycle;
  }

  public void Awake() {
    _districtCenter = GetComponent<DistrictCenter>();
    _districtCenterId = GetComponent<EntityComponent>()?.EntityId ?? System.Guid.Empty;
    _dispatchCenterRegistry.Register(this);
  }

  public void DeleteEntity() {
    _dispatchCenterRegistry.Unregister(this);
  }

  public void ClearRouteCache() {
    if (_routeDistanceCache.Count == 0) {
      return;
    }
    _routeDistanceCache.Clear();
    _performanceStats.CountRouteCacheClear();
  }

  public bool TryFindRoute(Accessible start, Accessible end, out float distance) {
    var key = RouteCacheKey(start, end);
    if (key.HasValue && _routeDistanceCache.TryGetValue(key.Value, out var cachedEntry)) {
      _performanceStats.CountRouteCacheHit();
      distance = cachedEntry.Distance;
      return cachedEntry.Found;
    }
    _performanceStats.CountRouteCacheMiss();
    var found = TransportPathDistance.TryFindRoadPath(start, end, out distance);
    if (key.HasValue) {
      // Cache misses too. Repeated impossible routes are just as expensive as repeated valid routes.
      _routeDistanceCache[key.Value] = new TransportRouteDistanceCacheEntry(found, distance);
    }
    return found;
  }

  public void RefreshSnapshot() {
    RefreshSnapshotInternal();
  }

  void RefreshSnapshotInternal() {
    _agents.Clear();
    _orders.Clear();
    if (_districtCenter?.DistrictPopulation == null) {
      return;
    }
    foreach (var worker in _districtCenter.DistrictPopulation.GetEnabledCharacters<Worker>()) {
      var section = _performanceStats.BeginSection();
      var agentSnapshot = CreateAgentSnapshot(worker);
      _performanceStats.EndAgentSection(section);
      if (!agentSnapshot.IsTransportAgent) {
        continue;
      }
      _agents.Add(agentSnapshot);
      _performanceStats.CountAgent();
      section = _performanceStats.BeginSection();
      if (TryCreateOrderSnapshot(worker, agentSnapshot, out var orderSnapshot)) {
        _orders.Add(orderSnapshot);
        _performanceStats.CountActiveOrder();
      } else {
        _progressByAgent.Remove(worker);
        _orderMemoryByAgent.Remove(worker);
      }
      _performanceStats.EndActiveOrderSection(section);
    }
    var refreshSection = _performanceStats.BeginSection();
    AddQueuedOrders();
    _performanceStats.EndQueuedOrderSection(refreshSection);
    refreshSection = _performanceStats.BeginSection();
    AddConstructionOrders();
    _performanceStats.EndConstructionOrderSection(refreshSection);
    refreshSection = _performanceStats.BeginSection();
    ClassifyOrderReadiness();
    _performanceStats.EndReadinessSection(refreshSection);
    refreshSection = _performanceStats.BeginSection();
    AddDecisions();
    _performanceStats.EndDecisionSection(refreshSection);
    refreshSection = _performanceStats.BeginSection();
    _agents.Sort((left, right) => left.EntityId.CompareTo(right.EntityId));
    _orders.Sort(CompareOrders);
    _performanceStats.EndSortSection(refreshSection);
  }

  void ClassifyOrderReadiness() {
    for (var i = 0; i < _orders.Count; i++) {
      _orders[i] = TransportOrderReadinessClassifier.Classify(_orders[i]);
    }
  }

  void AddDecisions() {
    for (var i = 0; i < _orders.Count; i++) {
      _performanceStats.CountDecisionOrder();
      var decision = _decisionEvaluator.Evaluate(_orders[i], _agents, this);
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
    var workRefuser = worker.GetComponent<WorkRefuser>();
    if (!goodCarrier || !goodReserver || !navigator || !workRefuser) {
      return TransportAgentSnapshot.NotTransportAgent(worker);
    }
    var worldPosition = navigator.CurrentAccessOrPosition();
    var position = NavigationCoordinateSystem.WorldToGridInt(worldPosition);
    var activity = TransportAgentActivity.Create(goodCarrier, goodReserver, behaviorManager, worker.JobRunning);
    var walkingSpeed = worker.GetComponent<WalkerSpeedManager>()?.GetWalkerSpeedAtCurrentPosition() ?? 0f;
    var entityId = worker.GetComponent<EntityComponent>()?.EntityId ?? System.Guid.Empty;
    var workplaceRole = ClassifyWorkplaceRole(worker);
    var role = ClassifyAgentRole(worker, behaviorManager, workplaceRole);
    var workplaceMarkedForEmptying = IsWorkplaceMarkedForEmptying(worker);
    return new TransportAgentSnapshot(
        entityId, worker, TransportAgentSnapshot.FormatWorker(worker), position, worldPosition, walkingSpeed,
        goodCarrier.LiftingCapacity, activity.State, role, workplaceRole, activity, workRefuser.RefusesWork,
        workplaceMarkedForEmptying, isTransportAgent: true);
  }

  static bool IsWorkplaceMarkedForEmptying(Worker worker) {
    var workplace = worker.Workplace;
    return workplace && workplace.GetComponent<Emptiable>()?.IsMarkedForEmptying == true;
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
        TransportWorkplaceRole.SpecializedResource => TransportAgentRole.SpecializedResource,
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
    if (HasSpecializedResourceBehavior(workplace)) {
      return TransportWorkplaceRole.SpecializedResource;
    }
    WarnUnknownWorkplace(worker, workplace);
    return TransportWorkplaceRole.Unknown;
  }

  bool TryCreateOrderSnapshot(
      Worker worker, TransportAgentSnapshot agentSnapshot, out TransportOrderSnapshot orderSnapshot) {
    var goodCarrier = worker.GetComponent<GoodCarrier>();
    var goodReserver = worker.GetComponent<GoodReserver>();
    var behaviorManager = worker.GetComponent<BehaviorManager>();
    if (TryCreateNeedOrderSnapshot(worker, agentSnapshot, goodReserver, behaviorManager, out orderSnapshot)) {
      return true;
    }
    if (!goodCarrier.IsCarrying && !goodReserver.HasReservedStock && !goodReserver.HasReservedCapacity) {
      orderSnapshot = default;
      return false;
    }
    var source = goodReserver.HasReservedStock ? goodReserver.StockReservation.Inventory : null;
    var target = goodReserver.HasReservedCapacity ? goodReserver.CapacityReservation.Inventory : null;
    if (!IsActiveTransportOrder(behaviorManager, goodCarrier, target)) {
      if (!IsKnownResourceReservation(behaviorManager)) {
        WarnIgnoredActiveTransport(worker, behaviorManager, goodCarrier, goodReserver, source, target);
      }
      orderSnapshot = default;
      return false;
    }
    var goodAmount = PickGoodAmount(goodCarrier, goodReserver);
    var phase = goodCarrier.IsCarrying ? OrderPhase.Delivering : OrderPhase.PickingUp;
    RestoreKnownEndpoints(worker, goodAmount, ref source, ref target);
    RestoreRequesterEndpoint(behaviorManager, ref source, ref target);
    RestoreWorkerOutputEndpoint(worker, goodAmount, target, ref source);
    RestoreDistrictEndpoint(goodAmount, ref source, ref target);
    var routeDistance = TryGetRouteDistance(source, target, out var distance) ? distance : float.NaN;
    var targetInventory = phase == OrderPhase.PickingUp ? source : target;
    var remainingDistance = TryGetRemainingDistance(worker, phase, targetInventory, agentSnapshot.WorldPosition, out var remaining)
        ? remaining
        : float.NaN;
    var remainingTaskHours = EstimateRemainingTaskHours(phase, routeDistance, remainingDistance, agentSnapshot.Speed);
    var progress = TrackProgress(worker, phase, targetInventory, goodAmount, remainingDistance);
    RememberKnownEndpoints(worker, source, target, goodAmount);
    orderSnapshot = TransportOrderSnapshot.Assigned(
        agentSnapshot.EntityId, worker, phase, source, target, goodAmount, routeDistance, remainingDistance,
        remainingTaskHours, progress);
    return true;
  }

  bool TryCreateNeedOrderSnapshot(
      Worker worker, TransportAgentSnapshot agentSnapshot, GoodReserver goodReserver, BehaviorManager behaviorManager,
      out TransportOrderSnapshot orderSnapshot) {
    if (!goodReserver.HasReservedStock
        || behaviorManager._runningBehavior is not InventoryNeedBehavior
        || behaviorManager._runningExecutor is not WalkInsideExecutor) {
      orderSnapshot = default;
      return false;
    }
    var source = goodReserver.StockReservation.Inventory;
    var goodAmount = goodReserver.StockReservation.GoodAmount;
    var remainingDistance = TryGetRemainingDistance(
        worker, OrderPhase.PickingUp, source, agentSnapshot.WorldPosition, out var remaining)
        ? remaining
        : float.NaN;
    var progress = TrackProgress(worker, OrderPhase.PickingUp, source, goodAmount, remainingDistance);
    var controlledBySmartHaulers = IsSmartCriticalNeed(worker, goodAmount);
    orderSnapshot = TransportOrderSnapshot.CriticalNeed(
        agentSnapshot.EntityId, worker, source, goodAmount, remainingDistance, progress, controlledBySmartHaulers);
    return true;
  }

  bool IsSmartCriticalNeed(Worker worker, GoodAmount goodAmount) {
    if (string.IsNullOrEmpty(goodAmount.GoodId)) {
      return false;
    }
    var needManager = worker.GetComponent<NeedManager>();
    if (!needManager) {
      return false;
    }
    foreach (var effect in _goodService.GetGood(goodAmount.GoodId).ConsumptionEffects) {
      if (SmartCriticalNeedIds.Contains(effect.NeedId) && needManager.NeedIsInCriticalState(effect.NeedId)) {
        return true;
      }
    }
    return false;
  }

  float EstimateRemainingTaskHours(OrderPhase phase, float routeDistance, float remainingDistance, float speed) {
    if (speed <= 0f || float.IsNaN(remainingDistance)) {
      return float.NaN;
    }
    var remainingTaskDistance = phase == OrderPhase.PickingUp
        ? RemainingPickupTaskDistance(routeDistance, remainingDistance)
        : remainingDistance;
    if (float.IsNaN(remainingTaskDistance)) {
      return float.NaN;
    }
    return _dayNightCycle.SecondsToHours(remainingTaskDistance / speed);
  }

  static float RemainingPickupTaskDistance(float routeDistance, float remainingDistance) {
    return float.IsNaN(routeDistance) ? float.NaN : remainingDistance + routeDistance;
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
    if (IsLaborTransportBehavior(behavior)) {
      return true;
    }
    if (behavior is WorkplaceBehavior workplaceBehavior) {
      return IsTransportWorkplaceBehavior(workplaceBehavior);
    }
    return false;
  }

  static bool IsKnownResourceReservation(BehaviorManager behaviorManager) {
    return TryGetRunningBehavior(behaviorManager, out var behavior) && behavior.GetType().Name is
        "GoodStackRetrieverBehavior"
        or "YieldRemoverBehavior";
  }

  static bool HasSpecializedResourceBehavior(Workplace workplace) {
    foreach (var workplaceBehavior in workplace.WorkplaceBehaviors) {
      if (IsSpecializedResourceBehavior(workplaceBehavior)) {
        return true;
      }
    }
    return false;
  }

  static void WarnUnknownWorkplace(Worker worker, Workplace workplace) {
    var key = $"workplace:{ComponentId(workplace)}:{workplace.GetType().FullName}";
    if (!LoggedUnknownWorkplaces.Add(key)) {
      return;
    }
    DebugEx.Warning(
        "SmartHaulers unknown workplace role: worker={0}, workplace={1}, workplaceType={2}, behaviors=[{3}].",
        TransportAgentSnapshot.FormatWorker(worker), DebugEx.ObjectToString(workplace),
        workplace.GetType().FullName, FormatWorkplaceBehaviors(workplace));
  }

  static string FormatWorkplaceBehaviors(Workplace workplace) {
    var behaviorNames = new List<string>();
    foreach (var workplaceBehavior in workplace.WorkplaceBehaviors) {
      behaviorNames.Add(workplaceBehavior.GetType().FullName);
    }
    return string.Join(", ", behaviorNames);
  }

  static void WarnIgnoredActiveTransport(
      Worker worker, BehaviorManager behaviorManager, GoodCarrier goodCarrier, GoodReserver goodReserver,
      Inventory source, Inventory target) {
    var behavior = behaviorManager ? behaviorManager._runningBehavior : null;
    var executor = behaviorManager ? behaviorManager._runningExecutor : null;
    var key = $"ignored:{ComponentId(worker)}:{behavior?.GetType().FullName}:{executor?.GetType().FullName}:"
        + $"{goodCarrier.IsCarrying}:{goodReserver.HasReservedStock}:{goodReserver.HasReservedCapacity}";
    if (!LoggedIgnoredActiveTransports.Add(key)) {
      return;
    }
    DebugEx.Warning(
        "SmartHaulers ignored active reservation: worker={0}, behavior={1}, executor={2}, carrying={3}, "
            + "reservedStock={4}, reservedCapacity={5}, source={6}, target={7}.",
        TransportAgentSnapshot.FormatWorker(worker), FormatObjectType(behavior), FormatObjectType(executor),
        FormatCarriedGood(goodCarrier), FormatReservedStock(goodReserver), FormatReservedCapacity(goodReserver),
        DebugEx.ObjectToString(source), DebugEx.ObjectToString(target));
  }

  static string FormatObjectType(object obj) {
    return obj?.GetType().FullName ?? "NULL";
  }

  static string FormatCarriedGood(GoodCarrier goodCarrier) {
    return goodCarrier.IsCarrying ? FormatGoodAmount(goodCarrier.CarriedGood.GoodAmount) : "none";
  }

  static string FormatReservedStock(GoodReserver goodReserver) {
    return goodReserver.HasReservedStock
        ? $"{FormatGoodAmount(goodReserver.StockReservation.GoodAmount)} at "
            + DebugEx.ObjectToString(goodReserver.StockReservation.Inventory)
        : "none";
  }

  static string FormatReservedCapacity(GoodReserver goodReserver) {
    return goodReserver.HasReservedCapacity
        ? $"{FormatGoodAmount(goodReserver.CapacityReservation.GoodAmount)} at "
            + DebugEx.ObjectToString(goodReserver.CapacityReservation.Inventory)
        : "none";
  }

  static string FormatGoodAmount(GoodAmount goodAmount) {
    return $"{goodAmount.Amount}x {goodAmount.GoodId}";
  }

  static bool IsSpecializedResourceBehavior(WorkplaceBehavior workplaceBehavior) {
    return workplaceBehavior.GetType().Name is
        "FarmHouseGoodStackRetrieverWorkplaceBehavior"
        or "GatherWorkplaceBehavior"
        or "LumberjackFlagWorkplaceBehavior"
        or "PlanterWorkplaceBehavior"
        or "ScavengerWorkplaceBehavior";
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

  static bool IsLaborTransportBehavior(Behavior behavior) {
    return behavior?.GetType().Name == "EmptyInventoriesLaborBehavior";
  }

  static bool IsTransportWorkplaceBehavior(WorkplaceBehavior workplaceBehavior) {
    var behaviorName = FormatBehaviorName(workplaceBehavior);
    return behaviorName == "BuilderHub" || IsBringBehavior(behaviorName) || IsTakeAwayBehavior(behaviorName);
  }

  void RestoreDistrictEndpoint(GoodAmount goodAmount, ref Inventory source, ref Inventory target) {
    var districtInventoryRegistry = _districtCenter.GetComponent<DistrictInventoryRegistry>();
    if (!districtInventoryRegistry) {
      return;
    }
    if (source && !target) {
      target = ClosestInventoryWithCapacity(source, goodAmount, districtInventoryRegistry);
    }
    if (!source && target) {
      source = ClosestInventoryWithStock(target, goodAmount.GoodId, districtInventoryRegistry);
    }
  }

  static void RestoreWorkerOutputEndpoint(Worker worker, GoodAmount goodAmount, Inventory target, ref Inventory source) {
    if (source && source != target || string.IsNullOrEmpty(goodAmount.GoodId)) {
      return;
    }
    var workplace = worker.Workplace;
    if (!workplace || !workplace.GetComponent<Manufactory>()) {
      return;
    }
    var inventories = workplace.GetComponent<Inventories>();
    if (inventories) {
      WarnIfManyInventories(workplace, inventories);
      source = FindGoodsOutputInventoryForGood(inventories.EnabledInventories, goodAmount.GoodId, target);
      return;
    }
    source = FindGoodsOutputInventoryForGood(workplace.GetComponentsAllocating<Inventory>(), goodAmount.GoodId, target);
  }

  static Inventory FindGoodsOutputInventoryForGood(
      IEnumerable<Inventory> inventories, string goodId, Inventory excludedInventory) {
    foreach (var inventory in inventories) {
      if (IsGoodsInventory(inventory) && inventory != excludedInventory && inventory.Gives(goodId)) {
        return inventory;
      }
    }
    return null;
  }

  Inventory ClosestInventoryWithCapacity(
      Inventory source, GoodAmount goodAmount, DistrictInventoryRegistry districtInventoryRegistry) {
    var sourceAccessible = source.GetEnabledComponent<Accessible>();
    if (!sourceAccessible) {
      return null;
    }
    Inventory closestInventory = null;
    var closestDistance = float.MaxValue;
    foreach (var inventory in districtInventoryRegistry.ActiveInventoriesWithCapacity(goodAmount.GoodId)) {
      var targetAccessible = inventory.GetEnabledComponent<Accessible>();
      if (inventory != source
          && targetAccessible
          && InventoryIsTaking(inventory, goodAmount)
          && TryFindRoute(sourceAccessible, targetAccessible, out var distance)
          && distance < closestDistance) {
        closestInventory = inventory;
        closestDistance = distance;
      }
    }
    return closestInventory;
  }

  Inventory ClosestInventoryWithStock(
      Inventory target, string goodId, DistrictInventoryRegistry districtInventoryRegistry) {
    var targetAccessible = target.GetEnabledComponent<Accessible>();
    if (!targetAccessible) {
      return null;
    }
    Inventory closestInventory = null;
    var closestDistance = float.MaxValue;
    foreach (var inventory in districtInventoryRegistry.ActiveInventoriesWithStock(goodId)) {
      var sourceAccessible = inventory.GetEnabledComponent<Accessible>();
      if (inventory != target
          && sourceAccessible
          && TryFindRoute(targetAccessible, sourceAccessible, out var distance)
          && distance < closestDistance) {
        closestInventory = inventory;
        closestDistance = distance;
      }
    }
    return closestInventory;
  }

  static bool InventoryIsTaking(Inventory inventory, GoodAmount goodAmount) {
    return inventory.HasUnreservedCapacity(goodAmount)
        && inventory.GetComponent<IInventoryValidator>().ValidInventory
        && inventory.GetComponent<BlockableObject>().IsUnblocked;
  }

  void AddQueuedOrders() {
    var districtBuildingRegistry = _districtCenter.GetComponent<DistrictBuildingRegistry>();
    if (!districtBuildingRegistry) {
      return;
    }
    foreach (var haulCandidate in districtBuildingRegistry.GetEnabledBuildings<HaulCandidate>()) {
      if (!haulCandidate.Enabled || !haulCandidate.GetComponent<BlockableObject>().IsUnblocked) {
        continue;
      }
      haulCandidate.GetWeightedBehaviors(_weightedBehaviors);
      foreach (var weightedBehavior in _weightedBehaviors) {
        if (weightedBehavior.Weight <= 0f) {
          continue;
        }
        var orderCount = _orders.Count;
        PossibleTransportOrderPlanner.AddPlans(
            _districtCenter, haulCandidate, weightedBehavior, _orders, _performanceStats, this);
        for (var i = orderCount; i < _orders.Count; i++) {
          _performanceStats.CountQueuedOrder();
        }
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
          _performanceStats.CountConstructionOrder();
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

  static TransportRouteDistanceCacheKey? RouteCacheKey(Accessible start, Accessible end) {
    var startId = ComponentId(start);
    var endId = ComponentId(end);
    if (startId == Guid.Empty || endId == Guid.Empty) {
      return null;
    }
    return new TransportRouteDistanceCacheKey(startId, endId);
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
      WarnIfManyInventories(requester, inventories);
      return PickInventory(inventories, behaviorName);
    }
    foreach (var inventory in requester.GetComponentsAllocating<Inventory>()) {
      if (IsGoodsInventory(inventory)) {
        return inventory;
      }
    }
    return null;
  }

  static Inventory PickInventory(Inventories inventories, string behaviorName) {
    foreach (var inventory in inventories.EnabledInventories) {
      if (IsGoodsInventory(inventory) && MatchesBehavior(inventory, behaviorName)) {
        return inventory;
      }
    }
    foreach (var inventory in inventories.EnabledInventories) {
      if (IsGoodsInventory(inventory)) {
        return inventory;
      }
    }
    return null;
  }

  static bool IsGoodsInventory(Inventory inventory) {
    return inventory.ComponentName != ConstructionSiteInventoryInitializer.InventoryComponentName;
  }

  static void WarnIfManyInventories(BaseComponent owner, Inventories inventories) {
    if (inventories.AllInventories.Count <= 2) {
      return;
    }
    var key = $"many-inventories:{ComponentId(owner)}:{owner.GetType().FullName}";
    if (!LoggedManyInventories.Add(key)) {
      return;
    }
    DebugEx.Warning(
        "SmartHaulers found a building with more than two inventories: owner={0}, ownerType={1}, inventories=[{2}].",
        DebugEx.ObjectToString(owner), owner.GetType().FullName, FormatInventories(inventories));
  }

  static string FormatInventories(Inventories inventories) {
    var names = new List<string>();
    foreach (var inventory in inventories.AllInventories) {
      names.Add($"{inventory.ComponentName}:{inventory.GetType().FullName}");
    }
    return string.Join(", ", names);
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
    var start = DispatchPerformanceStats.Timestamp();
    try {
      var sourceAccessible = source ? source.GetEnabledComponent<Accessible>() : null;
      var targetAccessible = target ? target.GetEnabledComponent<Accessible>() : null;
      if (sourceAccessible
          && targetAccessible
          && TryFindRoute(sourceAccessible, targetAccessible, out distance)) {
        return true;
      }
      distance = 0f;
      return false;
    } finally {
      _performanceStats.EndActiveRoutePath(start);
    }
  }

  bool TryGetRemainingDistance(Worker worker, OrderPhase phase, Inventory target, Vector3 position, out float distance) {
    var start = DispatchPerformanceStats.Timestamp();
    try {
      if (TransportWalkingDistance.TryGetRemainingDistance(worker, out distance)) {
        return true;
      }
      var accessible = target ? target.GetEnabledComponent<Accessible>() : null;
      if (accessible && accessible.FindPathUnlimitedRange(position, [], out distance)) {
        return true;
      }
      distance = 0f;
      return false;
    } finally {
      _performanceStats.EndRemainingPath(start, phase == OrderPhase.PickingUp);
    }
  }

}

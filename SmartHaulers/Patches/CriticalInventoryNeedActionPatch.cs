// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using HarmonyLib;
using IgorZ.SmartHaulers.Dispatching;
using Timberborn.BehaviorSystem;
using Timberborn.Buildings;
using Timberborn.Carrying;
using Timberborn.ConstructionSites;
using Timberborn.Demolishing;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.InventoryNeedSystem;
using Timberborn.Navigation;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSystem;
using Timberborn.Planting;
using Timberborn.SleepSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using Timberborn.Yielding;
using UnityEngine;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartHaulers.Patches;

[HarmonyPatch(typeof(DistrictNeedBehaviorService), nameof(DistrictNeedBehaviorService.PickBestAction))]
static class CriticalInventoryNeedActionPatch {
  const float CriticalInventoryNeedActionPoints = float.MaxValue;

  static readonly ImmutableHashSet<string> CriticalInventoryNeedIds =
      ImmutableHashSet.Create("Hunger", "Thirst", "Biofuel", "Power");
  static readonly HashSet<string> LoggedInventoryNeedWithoutPosition = [];

  static bool Prefix(
      DistrictNeedBehaviorService __instance, NeedManager needManager, Vector3 essentialActionPosition,
      float hoursLeftForNonEssentialActions, NeedFilter needFilter, ref AppraisedAction? __result) {
    if (!TryPickNearestCriticalInventoryAction(__instance, needManager, needFilter, out var action)) {
      return true;
    }
    __result = action;
    return false;
  }

  internal static bool TryPickNearestCriticalInventoryAction(
      DistrictNeedBehaviorService service, NeedManager needManager, out AppraisedAction action) {
    return TryPickNearestCriticalInventoryAction(service, needManager, NeedFilter.AnyNeed(), out action);
  }

  static bool TryPickNearestCriticalInventoryAction(
      DistrictNeedBehaviorService service, NeedManager needManager, NeedFilter needFilter, out AppraisedAction action) {
    var walker = needManager.GetComponent<Walker>();
    var bestDuration = float.MaxValue;
    NeedBehavior bestBehavior = null;
    ImmutableArray<string> bestNeeds = default;
    foreach (var group in service._needBehaviors.Values) {
      if (!TryGetCriticalNeeds(group.Needs, needManager, needFilter, out var criticalNeeds)) {
        continue;
      }
      foreach (var needBehavior in group.NeedBehaviors) {
        if (needBehavior is not InventoryNeedBehavior) {
          continue;
        }
        var actionPosition = needBehavior.ActionPosition(needManager);
        if (!actionPosition.HasValue) {
          WarnInventoryNeedWithoutPosition(needManager, needBehavior, criticalNeeds);
          continue;
        }
        var duration = walker.CalculateTravelTimeInHours(needManager.Transform.position, actionPosition.Value);
        if (duration >= bestDuration) {
          continue;
        }
        bestDuration = duration;
        bestBehavior = needBehavior;
        bestNeeds = criticalNeeds;
      }
    }
    if (bestBehavior == null) {
      action = default;
      return false;
    }
    action = new AppraisedAction(bestBehavior, bestNeeds, CriticalInventoryNeedActionPoints);
    return true;
  }

  static bool TryGetCriticalNeeds(
      ImmutableArray<string> needs, NeedManager needManager, NeedFilter needFilter,
      out ImmutableArray<string> criticalNeeds) {
    var builder = ImmutableArray.CreateBuilder<string>();
    foreach (var need in needs) {
      if (CriticalInventoryNeedIds.Contains(need)
          && needManager.NeedIsInCriticalState(need)
          && needFilter.Filter(need)) {
        builder.Add(need);
      }
    }
    criticalNeeds = builder.ToImmutable();
    return criticalNeeds.Length > 0;
  }

  static void WarnInventoryNeedWithoutPosition(
      NeedManager needManager, NeedBehavior needBehavior, ImmutableArray<string> needs) {
    var key = $"need-position:{needManager.GetComponent<EntityComponent>()?.EntityId}:{needBehavior.GetType().FullName}";
    if (!LoggedInventoryNeedWithoutPosition.Add(key)) {
      return;
    }
    DebugEx.Warning(
        "SmartHaulers critical inventory need has no action position: worker={0}, behavior={1}, needs=[{2}].",
        TransportAgentSnapshot.FormatWorker(needManager.GetComponent<Worker>()),
        needBehavior.GetType().FullName, string.Join(", ", needs));
  }
}

[HarmonyPatch(typeof(BehaviorManager), nameof(BehaviorManager.Tick))]
static class CriticalInventoryNeedReroutePatch {
  const float RerouteImprovementToleranceHours = 0.01f;
  // Prototype tuning: short pickup tasks should finish instead of churning reservations; long trips yield to survival.
  const float CriticalNeedPickupInterruptionThresholdHours = 2f;
  static readonly HashSet<string> LoggedPickupEvaluationFailures = [];
  static readonly HashSet<string> LoggedGenericWalkingInterruptions = [];
  static readonly HashSet<string> LoggedInventoryAccessFailures = [];

  static void Prefix(BehaviorManager __instance) {
    if (!IsInterruptibleWalk(__instance, out var currentBehavior, out var currentTransport)) {
      return;
    }
    var needManager = __instance.GetComponent<NeedManager>();
    var citizen = __instance.GetComponent<Citizen>();
    if (!citizen.HasAssignedDistrict) {
      return;
    }
    var needBehaviorService = citizen.AssignedDistrict.GetComponent<DistrictNeedBehaviorService>();
    if (!CriticalInventoryNeedActionPatch.TryPickNearestCriticalInventoryAction(
            needBehaviorService, needManager, out var action)) {
      return;
    }
    if (ReferenceEquals(action.NeedBehavior, currentBehavior)) {
      return;
    }
    if (CurrentInventoryNeedIsNoWorse(__instance, needManager, currentBehavior, action)) {
      return;
    }
    if (currentTransport == CurrentTransport.Pickup
        && !PickupTransportIsWorthInterrupting(__instance, needManager, currentBehavior)) {
      return;
    }
    WarnGenericWalkingInterruption(__instance, currentBehavior, currentTransport);
    LogSmartInterruption(__instance, needManager, currentBehavior, currentTransport, action);
    __instance.GetComponent<GoodReserver>().UnreserveStock();
    __instance.GetComponent<GoodReserver>().UnreserveCapacity();
    __instance.GetComponent<Enterer>().UnreserveSlot();
    ReleaseMovementReservations(__instance);
    __instance.GetComponent<Walker>().StopNextTick();
    if (__instance._runningBehavior is SleepNeedBehavior sleepNeedBehavior) {
      sleepNeedBehavior._walkedToSleepingPosition = false;
    }
    __instance._runningBehavior = null;
    __instance._runningExecutor = null;
    __instance._returnToBehavior = false;
  }

  static bool IsInterruptibleWalk(
      BehaviorManager behaviorManager, out Behavior currentBehavior, out CurrentTransport currentTransport) {
    currentBehavior = behaviorManager._runningBehavior;
    currentTransport = ClassifyCurrentTransport(behaviorManager);
    if (currentBehavior == null || !IsMovementExecutor(behaviorManager._runningExecutor)) {
      return false;
    }
    if (IsNeverInterruptibleBehavior(currentBehavior)) {
      return false;
    }
    if (currentBehavior is NeedBehavior or EssentialNeedBehavior) {
      return true;
    }
    // Non-need walking work can be interrupted for survival; pickup transport is allowed only behind a time gate.
    return currentTransport != CurrentTransport.Delivery;
  }

  static bool IsMovementExecutor(IExecutor executor) {
    // Timberborn has several movement executors: WalkInside, WalkToPosition, WalkToAccessible, WalkToReservable, etc.
    return executor?.GetType().Name.StartsWith("Walk") == true;
  }

  static CurrentTransport ClassifyCurrentTransport(BehaviorManager behaviorManager) {
    var goodCarrier = behaviorManager.GetComponent<GoodCarrier>();
    var goodReserver = behaviorManager.GetComponent<GoodReserver>();
    if (goodCarrier && goodCarrier.IsCarrying) {
      return CurrentTransport.Delivery;
    }
    if (goodReserver && goodReserver.HasReservedStock) {
      return CurrentTransport.Pickup;
    }
    // Capacity-only walking means the agent still carries nothing. Resource jobs reserve their workplace inventory
    // before walking to a yield, and those trips are safe to interrupt for critical survival needs.
    return CurrentTransport.None;
  }

  static bool CurrentInventoryNeedIsNoWorse(
      BehaviorManager behaviorManager, NeedManager needManager, Behavior currentBehavior, AppraisedAction action) {
    var goodReserver = behaviorManager.GetComponent<GoodReserver>();
    if (currentBehavior is not InventoryNeedBehavior currentInventoryNeed
        || action.NeedBehavior is not NeedBehavior newNeedBehavior
        || !goodReserver.HasReservedStock) {
      return false;
    }
    var currentPosition = currentInventoryNeed.ActionPosition(needManager);
    var newPosition = newNeedBehavior.ActionPosition(needManager);
    if (!currentPosition.HasValue || !newPosition.HasValue) {
      return false;
    }
    var walker = behaviorManager.GetComponent<Walker>();
    var agentPosition = needManager.Transform.position;
    var currentDuration = walker.CalculateTravelTimeInHours(agentPosition, currentPosition.Value);
    var newDuration = walker.CalculateTravelTimeInHours(agentPosition, newPosition.Value);
    // The current inventory need disappears from the district service while its only stock is reserved by this agent.
    // Compare against the reserved current target explicitly, otherwise reroute can release and reacquire every tick.
    return newDuration + RerouteImprovementToleranceHours >= currentDuration;
  }

  static bool PickupTransportIsWorthInterrupting(
      BehaviorManager behaviorManager, NeedManager needManager, Behavior currentBehavior) {
    var goodReserver = behaviorManager.GetComponent<GoodReserver>();
    if (currentBehavior is InventoryNeedBehavior) {
      return true;
    }
    if (!goodReserver.HasReservedStock || !goodReserver.HasReservedCapacity) {
      WarnPickupEvaluationFailure(
          behaviorManager, currentBehavior, "missing stock or capacity reservation",
          goodReserver.HasReservedStock ? goodReserver.StockReservation.Inventory : null,
          goodReserver.HasReservedCapacity ? goodReserver.CapacityReservation.Inventory : null);
      return false;
    }
    if (!TryGetAccessPosition(goodReserver.StockReservation.Inventory, out var sourcePosition)
        || !TryGetAccessPosition(goodReserver.CapacityReservation.Inventory, out var targetPosition)) {
      WarnPickupEvaluationFailure(
          behaviorManager, currentBehavior, "missing inventory access position",
          goodReserver.StockReservation.Inventory, goodReserver.CapacityReservation.Inventory);
      return false;
    }
    var walker = behaviorManager.GetComponent<Walker>();
    var agentPosition = needManager.Transform.position;
    var pickupHours = walker.CalculateTravelTimeInHours(agentPosition, sourcePosition);
    var deliveryHours = walker.CalculateTravelTimeInHours(sourcePosition, targetPosition);
    return pickupHours + deliveryHours > CriticalNeedPickupInterruptionThresholdHours;
  }

  static void ReleaseMovementReservations(BehaviorManager behaviorManager) {
    // Work behaviors often reserve their destination before walking there; clearing BehaviorManager is not enough.
    behaviorManager.GetComponent<Planter>()?.Unreserve();
    behaviorManager.GetComponent<YielderRemover>()?.Unreserve();
    behaviorManager.GetComponent<Builder>()?.Unreserve();
    behaviorManager.GetComponent<Demolisher>()?.Unreserve();
  }

  static bool TryGetAccessPosition(Inventory inventory, out Vector3 position) {
    var accessible = inventory ? inventory.GetComponent<BuildingAccessible>()?.Accessible : null;
    Vector3? accessPosition = null;
    try {
      accessPosition = accessible ? accessible.UnblockedSingleAccess : null;
    } catch (InvalidOperationException e) {
      WarnInventoryAccessFailure(inventory, accessible, e);
    }
    if (accessPosition.HasValue) {
      position = accessPosition.Value;
      return true;
    }
    if (inventory) {
      position = inventory.Transform.position;
      return true;
    }
    position = default;
    return false;
  }

  static void WarnInventoryAccessFailure(Inventory inventory, Accessible accessible, InvalidOperationException e) {
    var key = $"inventory-access:{inventory.GetComponent<EntityComponent>()?.EntityId}:{e.Message}";
    if (!LoggedInventoryAccessFailures.Add(key)) {
      return;
    }
    DebugEx.Warning(
        "SmartHaulers cannot use inventory access position: inventory={0}, accessible={1}, error={2}.",
        DebugEx.ObjectToString(inventory), DebugEx.ObjectToString(accessible), e.Message);
  }

  static void WarnPickupEvaluationFailure(
      BehaviorManager behaviorManager, Behavior currentBehavior, string reason, Inventory source, Inventory target) {
    var key = $"pickup-eval:{behaviorManager.GetComponent<EntityComponent>()?.EntityId}:"
        + $"{currentBehavior?.GetType().FullName}:{reason}";
    if (!LoggedPickupEvaluationFailures.Add(key)) {
      return;
    }
    var goodCarrier = behaviorManager.GetComponent<GoodCarrier>();
    var goodReserver = behaviorManager.GetComponent<GoodReserver>();
    DebugEx.Warning(
        "SmartHaulers cannot evaluate critical-need pickup interruption: worker={0}, reason={1}, behavior={2}, "
            + "executor={3}, carrying={4}, reservedStock={5}, reservedCapacity={6}, source={7}, target={8}.",
        TransportAgentSnapshot.FormatWorker(behaviorManager.GetComponent<Worker>()), reason,
        currentBehavior?.GetType().FullName ?? "NULL",
        behaviorManager._runningExecutor?.GetType().FullName ?? "NULL",
        goodCarrier && goodCarrier.IsCarrying ? FormatGoodAmount(goodCarrier.CarriedGood.GoodAmount) : "none",
        goodReserver && goodReserver.HasReservedStock
            ? FormatGoodAmount(goodReserver.StockReservation.GoodAmount)
            : "none",
        goodReserver && goodReserver.HasReservedCapacity
            ? FormatGoodAmount(goodReserver.CapacityReservation.GoodAmount)
            : "none",
        DebugEx.ObjectToString(source), DebugEx.ObjectToString(target));
  }

  static void LogSmartInterruption(
      BehaviorManager behaviorManager, NeedManager needManager, Behavior currentBehavior, CurrentTransport currentTransport,
      AppraisedAction action) {
    Vector3? targetPosition = action.NeedBehavior is NeedBehavior needBehavior
        ? needBehavior.ActionPosition(needManager)
        : null;
    DebugEx.Info(
        "SmartHaulers interrupts current movement for a critical need: worker={0}, behavior={1}, executor={2}, "
            + "transport={3}, needs=[{4}], newBehavior={5}, targetPosition={6}.",
        TransportAgentSnapshot.FormatWorker(behaviorManager.GetComponent<Worker>()),
        currentBehavior.GetType().FullName,
        behaviorManager._runningExecutor?.GetType().FullName ?? "NULL",
        currentTransport,
        string.Join(", ", action.AffectedNeeds),
        action.NeedBehavior.GetType().FullName,
        targetPosition.HasValue ? targetPosition.Value.ToString() : "NULL");
  }

  static void WarnGenericWalkingInterruption(
      BehaviorManager behaviorManager, Behavior currentBehavior, CurrentTransport currentTransport) {
    if (currentBehavior is NeedBehavior or EssentialNeedBehavior
        || currentTransport != CurrentTransport.None
        || IsKnownReleasedWalkingBehavior(currentBehavior)) {
      return;
    }
    var key = $"walking-interrupt:{currentBehavior.GetType().FullName}:"
        + $"{behaviorManager._runningExecutor?.GetType().FullName}";
    if (!LoggedGenericWalkingInterruptions.Add(key)) {
      return;
    }
    DebugEx.Warning(
        "SmartHaulers interrupts unclassified walking behavior for a critical need: worker={0}, behavior={1}, "
            + "executor={2}. If this behavior owns reservations, add an explicit release path.",
        TransportAgentSnapshot.FormatWorker(behaviorManager.GetComponent<Worker>()),
        currentBehavior.GetType().FullName, behaviorManager._runningExecutor?.GetType().FullName ?? "NULL");
  }

  static bool IsNeverInterruptibleBehavior(Behavior behavior) {
    return behavior.GetType().Name == "DieRootBehavior";
  }

  static bool IsKnownReleasedWalkingBehavior(Behavior behavior) {
    return behavior.GetType().Name is
        "BuildBehavior"
        or "DemolishBehavior"
        or "PlantBehavior"
        or "ProduceWorkplaceBehavior"
        or "WaitInsideIdlyWorkplaceBehavior"
        or "WanderRootBehavior"
        or "YieldRemoverBehavior";
  }

  static string FormatGoodAmount(GoodAmount goodAmount) {
    return $"{goodAmount.Amount}x {goodAmount.GoodId}";
  }

  enum CurrentTransport {
    None,
    Pickup,
    Delivery,
  }
}

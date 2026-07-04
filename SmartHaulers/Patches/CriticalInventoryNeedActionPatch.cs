// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Immutable;
using HarmonyLib;
using Timberborn.BehaviorSystem;
using Timberborn.EnterableSystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.InventoryNeedSystem;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSystem;
using Timberborn.WalkingSystem;
using UnityEngine;

namespace IgorZ.SmartHaulers.Patches;

[HarmonyPatch(typeof(DistrictNeedBehaviorService), nameof(DistrictNeedBehaviorService.PickBestAction))]
static class CriticalInventoryNeedActionPatch {
  static readonly ImmutableHashSet<string> CriticalInventoryNeedIds =
      ImmutableHashSet.Create("Hunger", "Thirst", "Biofuel", "Power");

  static bool Prefix(
      DistrictNeedBehaviorService __instance, NeedManager needManager, Vector3 essentialActionPosition,
      float hoursLeftForNonEssentialActions, NeedFilter needFilter, ref AppraisedAction? __result) {
    if (!needFilter.OnlyCriticalStateNeeds) {
      return true;
    }
    if (!TryPickNearestCriticalInventoryAction(__instance, needManager, out var action)) {
      return true;
    }
    __result = action;
    return false;
  }

  internal static bool TryPickNearestCriticalInventoryAction(
      DistrictNeedBehaviorService service, NeedManager needManager, out AppraisedAction action) {
    var walker = needManager.GetComponent<Walker>();
    var bestDuration = float.MaxValue;
    NeedBehavior bestBehavior = null;
    ImmutableArray<string> bestNeeds = default;
    foreach (var group in service._needBehaviors.Values) {
      if (!TryGetCriticalNeeds(group.Needs, needManager, out var criticalNeeds)) {
        continue;
      }
      foreach (var needBehavior in group.NeedBehaviors) {
        if (needBehavior is not InventoryNeedBehavior) {
          continue;
        }
        var actionPosition = needBehavior.ActionPosition(needManager);
        if (!actionPosition.HasValue) {
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
    action = new AppraisedAction(bestBehavior, bestNeeds, points: 1f);
    return true;
  }

  static bool TryGetCriticalNeeds(
      ImmutableArray<string> needs, NeedManager needManager, out ImmutableArray<string> criticalNeeds) {
    var builder = ImmutableArray.CreateBuilder<string>();
    foreach (var need in needs) {
      if (CriticalInventoryNeedIds.Contains(need) && needManager.NeedIsInCriticalState(need)) {
        builder.Add(need);
      }
    }
    criticalNeeds = builder.ToImmutable();
    return criticalNeeds.Length > 0;
  }
}

[HarmonyPatch(typeof(BehaviorManager), nameof(BehaviorManager.Tick))]
static class CriticalInventoryNeedReroutePatch {
  static void Prefix(BehaviorManager __instance) {
    if (__instance._runningBehavior is not InventoryNeedBehavior currentBehavior
        || __instance._runningExecutor is not WalkInsideExecutor) {
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
    __instance.GetComponent<GoodReserver>().UnreserveStock();
    __instance.GetComponent<Enterer>().UnreserveSlot();
    __instance.GetComponent<Walker>().StopNextTick();
    __instance._runningBehavior = null;
    __instance._runningExecutor = null;
    __instance._returnToBehavior = false;
  }
}

// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WellbeingFirst {

// FIXME: the logic must go to a special component on character to avoid many get components calls.
[HarmonyPatch(typeof(DistrictNeedBehaviorService), nameof(DistrictNeedBehaviorService.PickShortestAction))]
static class DistrictNeedBehaviorServicePatch {
  static void Prefix(bool __runOriginal, NeedManager needManager, ref Vector3 essentialActionPosition, ref HaulerWellbeingOptimizer __state) {
    if (!__runOriginal) {
      return; // The other patches must follow the same style to properly support the skip logic!
    }
    __state = null;
    var optimizer = needManager.GetComponentFast<HaulerWellbeingOptimizer>();
    if (!optimizer.NeedsOptimization) {
      return;
    }

    __state = optimizer;
    // var bestPosition = optimizer.GetEssentialPosition();
    // if (Vector3.Distance(essentialActionPosition, bestPosition) > 10) {
    //   //FIXME
    //   DebugEx.Warning(
    //       "*** name={0}, essentialPos={1}, beaverPos={2} => use characters pos",
    //       optimizer.Character.FirstName, essentialActionPosition, bestPosition);
    // }
    essentialActionPosition = optimizer.GetEssentialPosition();
  }

  static void Postfix(bool __runOriginal,
                      Vector3 essentialActionPosition,
                      SortedSet<DistrictNeedBehaviorService.AppraisedNeedBehaviorGroup> ____appraisedNeedBehaviors,
                      ref AppraisedAction? __result, ref HaulerWellbeingOptimizer __state) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!__state) {
      return;  // The citizen is not subject to optimization.
    }
    if (!__result.HasValue) {
      return;  // No result, no improvement.
    }
    var appraisedAction = __result.Value;
    var optimizer = __state;
    var criticalNeed = appraisedAction.AffectedNeeds.First();
    if (!optimizer.CriticalNeedsForRole.Contains(criticalNeed)) {
      return;  // Nothing to optimize.
    }

    //FIXME
    // DebugEx.Warning("*** Check for a second opinion: character={0}, criticalNeed={1}",
    //                 optimizer.Character.FirstName, criticalNeed);
    // PrintAppraisedBehaviors(____appraisedNeedBehaviors, optimizer, essentialActionPosition);

    // For hunger there can be may choices, check how different are the distances.
    var (alternative, durationDelta) = GetBestActionForNeed(
        criticalNeed, ____appraisedNeedBehaviors, optimizer, essentialActionPosition);
    if (!alternative.HasValue || durationDelta < Features.HaulerPathDurationDifferenceThreshold) {
      return;
    }
    var alternativeAction = alternative.Value;
    if (alternativeAction.NeedBehavior == appraisedAction.NeedBehavior) {
      return;  // We didn't improve it.
    }

    DebugEx.Warning("*** Found a better alternative for {6}: name={0}, was={1} (wanted:{2}), now={3} (wanted:{4}), durationDelta={5}",
                    optimizer.Character.FirstName,
                    DebugEx.ObjectToString(appraisedAction.NeedBehavior), DebugEx.C2S(appraisedAction.AffectedNeeds),
                    DebugEx.ObjectToString(alternative.Value.NeedBehavior), DebugEx.C2S(alternative.Value.AffectedNeeds),
                    durationDelta, criticalNeed);
    __result = alternative.Value;
  }

  //FIXME: get piosition from optimizer? 
  static (AppraisedAction? action, float durationDelta) GetBestActionForNeed(
      string need, SortedSet<DistrictNeedBehaviorService.AppraisedNeedBehaviorGroup> appraisedNeedBehaviors,
      HaulerWellbeingOptimizer optimizer, Vector3 essentialActionPosition) {
    AppraisedAction? bestActionForNeed = null;
    var durationCalculator = optimizer.DurationCalculator;
    var needManager = optimizer.NeedManager;
    var minimumDuration = float.MaxValue;
    var firstDuration = -1f;

    var restrictedGoods = optimizer.RestrictedGoods;
    
    foreach (var appraisedNeedBehavior in appraisedNeedBehaviors) {
      if (appraisedNeedBehavior.NeedBehaviorGroup.Needs.First() != need) {
        continue;
      }
      var needBehaviorGroup = appraisedNeedBehavior.NeedBehaviorGroup;
      var needBehaviors = needBehaviorGroup.NeedBehaviors;
      for (var i = needBehaviors.Count - 1; i >= 0; i--) {
        var needBehavior = needBehaviors[i];
        var actionPos = needBehavior.ActionPosition(needManager);
        if (!actionPos.HasValue) {
          continue;
        }
        var duration = durationCalculator.DurationWithReturnInHours(actionPos.Value, essentialActionPosition);
        if (duration < minimumDuration) {
          if (needBehaviorGroup.Needs.Any(x => restrictedGoods.Contains(x))) {
            //FIXME
            DebugEx.Warning("*** reject candidate due to good restriction: options={0}, restrictions={1}",
                DebugEx.C2S(needBehaviorGroup.Needs), DebugEx.C2S(restrictedGoods));
            continue;  // Optimizer doesn't want this good to be consumed.
          }

          bestActionForNeed = new AppraisedAction(needBehavior, needBehaviorGroup.Needs, appraisedNeedBehavior.Points);
          minimumDuration = duration;
          if (firstDuration < 0) {
            firstDuration = duration;
          }
        }
      }
    }
    return (bestActionForNeed, firstDuration - minimumDuration);
  }

  static void PrintAppraisedBehaviors(
      SortedSet<DistrictNeedBehaviorService.AppraisedNeedBehaviorGroup> behaviorGroups,
      HaulerWellbeingOptimizer optimizer, Vector3 essentialActionPosition) {
    foreach (var group in behaviorGroups) {
      var behaviors = group.NeedBehaviorGroup.NeedBehaviors;
      DebugEx.Warning(
          "*** Group: needs={0}, behaviorsCount={1}, points={2}", DebugEx.C2S(group.NeedBehaviorGroup.Needs),
          behaviors.Count, group.Points);
      foreach (var beh in behaviors) {
        var behaviorPos = beh.ActionPosition(optimizer.NeedManager);
        float distance;
        if (behaviorPos.HasValue) {
          distance = optimizer.DurationCalculator.DurationWithReturnInHours(behaviorPos.Value, essentialActionPosition);
        } else {
          distance = float.NaN;
        }
        DebugEx.Warning("Behavior: {0}{1}, distance={2}", beh.ComponentName, DebugEx.ObjectToString(beh), distance);
      }
    }
  }
}

}

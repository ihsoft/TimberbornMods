// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using IgorZ.TimberCommons.Common;
using Timberborn.BeaverBehavior;
using Timberborn.BehaviorSystem;
using Timberborn.Characters;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.GoodConsumingBuildingSystemUI;
using Timberborn.NeedBehaviorSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.WellbeingFirst {

static class OverridenNeedBehaviorPicker {
  static readonly HashSet<string> ImportantNeeds = new() { "Hunger", "Thirst" };

  public static bool PickBestBehavior(BeaverNeedBehaviorPicker instance, ref Behavior result) {
    var characterName = instance.GetComponentFast<Character>().FirstName;
    // if (characterName != "Alvemoh" && characterName != "Teja") {
    //   return false;
    // }
    if (!CheckIfHasSpeedImpactNeed(instance)) {
      return false;  // Everything looks right.
    }
    
    var currentPosition = instance.TransformFast.position;
    AppraisedAction ourBestAction = instance.GetBestNonEssentialAction(currentPosition, 24, NeedFilter.NeedIsCritical(instance._needManager));
    var ourBehaviorName = FormatTarget(ourBestAction.NeedBehavior);
    var pickNeed = ourBestAction.NeedBehavior ? ourBestAction.AffectedNeeds.First() : null;
    var ourNeeds = ourBestAction.NeedBehavior ? DebugEx.C2S(ourBestAction.AffectedNeeds) : null;
    DebugEx.Warning("*** Override stock decision: name={0}, behavior={1}, needs={2}", characterName, ourBehaviorName, ourNeeds);
    instance._needsBeingCriticallySatisfied.Clear();
    instance.AddCriticallySatisfiedNeeds(ourBestAction.AffectedNeeds);
    result = ourBestAction.NeedBehavior;
    return true;
  }

  static string FormatTarget(Behavior behavior) {
    if (!behavior) {
      return "NULL";
    }
    return $"{behavior.ComponentName}{DebugEx.ObjectToString(behavior)}";
  }

  static bool CheckIfHasSpeedImpactNeed(BeaverNeedBehaviorPicker instance) {
    var anyNeed = false;
    foreach (var need in instance._needManager._needs._needArray) {
      if (need.Points < 0 && ImportantNeeds.Contains(need.Specification.Id)) {
        // DebugEx.Warning("*** needs second opinion: beaver={0}, need={1}, points={2}",
        //                 instance.GetComponentFast<Character>().FirstName,
        //                 need.Specification.Id, need.Points);
        anyNeed = true;
        //return true;
      }
    }
    return anyNeed;
    //return false;
  }
}

[HarmonyPatch(typeof(BeaverNeedBehaviorPicker), nameof(BeaverNeedBehaviorPicker.GetBestNeedBehavior), new Type[]{})]
static class GetBestNeedBehaviorPatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix(bool __runOriginal, BeaverNeedBehaviorPicker __instance, ref Behavior __result) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }
    return !OverridenNeedBehaviorPicker.PickBestBehavior(__instance, ref __result);
  }
}

[HarmonyPatch(typeof(BeaverNeedBehaviorPicker), nameof(BeaverNeedBehaviorPicker.GetBestNeedBehaviorAffectingNeedsInCriticalState))]
static class GetBestNeedBehaviorPatch2 {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix(bool __runOriginal, BeaverNeedBehaviorPicker __instance, ref Behavior __result) {
    if (!__runOriginal) {
      return false; // The other patches must follow the same style to properly support the skip logic!
    }
    return !OverridenNeedBehaviorPicker.PickBestBehavior(__instance, ref __result);
  }
}

}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.ConstructionSites;

namespace IgorZ.Automation.PathCheckingSystem;

/// <summary>Holds construction site finishing until the path checking service allows it.</summary>
[HarmonyPatch(typeof(ConstructionSite), nameof(ConstructionSite.FinishIfRequirementsMet))]
static class ConstructionSiteFinishIfRequirementsMetPatch {
  // ReSharper disable once UnusedMember.Local
  static bool Prefix(bool __runOriginal, ConstructionSite __instance) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!__instance.IsReadyToFinish || !__instance.IsFinishNotBlocked) {
      return true;
    }
    var site = __instance.GetComponentFast<PathCheckingSite>();
    if (site && site.enabled) {
      PathCheckingService.Instance.CheckBlockingStateAndTriggerActions(site);
      return site.CanFinish;
    }
    return true;
  }
}
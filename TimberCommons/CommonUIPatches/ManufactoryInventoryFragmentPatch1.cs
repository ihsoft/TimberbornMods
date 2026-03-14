// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.CoreUI;
using Timberborn.WorkshopsUI;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;
using ProgressBar = Timberborn.CoreUI.ProgressBar;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

/// <summary>Harmony patch that adds "supply left" element to the manufactory UI fragment.</summary>
/// <remarks>It only adds an element, but the actual handling is not made here.</remarks>
/// <seealso cref="ManufactoryInventoryFragmentPatch2"/>
[HarmonyPatch(typeof(ManufactoryInventoryFragment), nameof(ManufactoryInventoryFragment.InitializeFragment))]
static class ManufactoryInventoryFragmentPatch1 {
  const string GoodConsumingFragmentElementName = "Game/EntityPanel/GoodConsumingBuildingFragment";

  // ReSharper disable once UnusedMember.Local
  static void Postfix(bool __runOriginal, VisualElement __result, VisualElementLoader ____visualElementLoader) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    var element = ____visualElementLoader.LoadVisualElement(GoodConsumingFragmentElementName);
    if (element == null) {
      DebugEx.Warning("Cannot find GoodsConsumingBuildingFragment UI", GoodConsumingFragmentElementName);
      return;
    }
    var hoursLeftBar = element.Q<ProgressBar>("ProgressBar");
    var hoursLeftLabel = element.Q<Label>("HoursLeft");
    ManufactoryInventoryFragmentPatch2.HoursLeftBar = hoursLeftBar;
    ManufactoryInventoryFragmentPatch2.HoursLeftLabel = hoursLeftLabel;
    if (hoursLeftBar == null || hoursLeftLabel == null) {
      DebugEx.Warning("Cannot copy GoodsConsumingBuildingFragment UI into ManufactoryInventoryFragment");
      return;
    }
    hoursLeftBar.ToggleDisplayStyle(visible: false);
    __result.Insert(0, hoursLeftBar);
  }
}
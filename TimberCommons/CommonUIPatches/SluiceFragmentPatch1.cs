// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using TimberApi.DependencyContainerSystem;
using Timberborn.WaterSystem;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches {

[HarmonyPatch]
static class SluiceFragmentPatch1 {
  internal static Label FlowLabel;
  internal static IThreadSafeWaterMap ThreadSafeWaterMap;
  
  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod("Timberborn.WaterBuildingsUI.SluiceFragment:InitializeFragment");
  }

  static void Postfix(bool __runOriginal, VisualElement ____contaminationLabel) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (!WaterBuildingsSettings.ShowCurrentStrengthInSluice) {
      return;
    }
    ThreadSafeWaterMap = DependencyContainer.GetInstance<IThreadSafeWaterMap>();
    var uiFactory = DependencyContainer.GetInstance<UiFactory>();
    FlowLabel = uiFactory.CreateLabel();
    ____contaminationLabel.parent.Add(FlowLabel);
  }
}

}

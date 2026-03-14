// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.WaterBuildingsUI;
using Timberborn.WaterSystem;
using UnityEngine.UIElements;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonUIPatches;

[HarmonyPatch(typeof(SluiceFragment), nameof(SluiceFragment.InitializeFragment))]
static class SluiceFragmentPatch1 {
  internal static Label FlowLabel;
  internal static IThreadSafeWaterMap ThreadSafeWaterMap;

  static void Postfix(bool __runOriginal, VisualElement ____contaminationLabel) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    ThreadSafeWaterMap = StaticBindings.DependencyContainer.GetInstance<IThreadSafeWaterMap>();
    var uiFactory = StaticBindings.DependencyContainer.GetInstance<UiFactory>();
    FlowLabel = uiFactory.CreateLabel();
    ____contaminationLabel.parent.Add(FlowLabel);
  }
}

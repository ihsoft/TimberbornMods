// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.UIFormatters;
using Timberborn.WaterBuildings;
using Timberborn.WaterBuildingsUI;
using Timberborn.WaterSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

[HarmonyPatch(typeof(ThrottlingValveFragment))]
static class ThrottlingValveFragmentPatch {
  const string CurrentFlowLabelName = "TimberCommonsCurrentFlow";
  const string CurrentFlowLocKey = "IgorZ.TimberCommons.WaterBuildings.ThrottlingValve.CurrentFlow";

  static ILoc _loc = null!;
  static IThreadSafeWaterMap _threadSafeWaterMap = null!;
  static Label _currentFlowLabel;
  static readonly Phrase CurrentFlowPhrase = Phrase.New(CurrentFlowLocKey).FormatFlow<float>("F2");

  public static void SetServices(ILoc loc, IThreadSafeWaterMap threadSafeWaterMap) {
    _loc = loc;
    _threadSafeWaterMap = threadSafeWaterMap;
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(ThrottlingValveFragment.InitializeFragment))]
  static void InitializeFragmentPostfix(VisualElement __result) {
    var currentFlowLabel = __result.Q<Label>(CurrentFlowLabelName);
    if (currentFlowLabel != null) {
      _currentFlowLabel = currentFlowLabel;
      return;
    }

    var outflowLimitSlider = __result.Q<PreciseSlider>("OutflowLimitSlider");
    if (outflowLimitSlider?.parent == null) {
      return;
    }

    currentFlowLabel = new Label {
        name = CurrentFlowLabelName,
    };
    currentFlowLabel.AddToClassList("entity-panel__text");
    currentFlowLabel.AddToClassList("game-text-small");
    currentFlowLabel.AddToClassList("text--centered");
    currentFlowLabel.style.marginBottom = 4;
    outflowLimitSlider.parent.Insert(outflowLimitSlider.parent.IndexOf(outflowLimitSlider) + 1, currentFlowLabel);
    _currentFlowLabel = currentFlowLabel;
  }

  [HarmonyPostfix]
  [HarmonyPatch(nameof(ThrottlingValveFragment.UpdateFragment))]
  static void UpdateFragmentPostfix(ThrottlingValveFragment __instance) {
    var label = GetCurrentFlowLabel(__instance._root);
    if (label == null) {
      return;
    }

    var throttlingValve = __instance._throttlingValve;
    label.ToggleDisplayStyle(throttlingValve);
    if (!throttlingValve) {
      return;
    }

    label.text = _loc.T(CurrentFlowPhrase, GetValveFlow(throttlingValve));
  }

  static Label GetCurrentFlowLabel(VisualElement root) {
    if (_currentFlowLabel != null) {
      return _currentFlowLabel;
    }
    if (root == null) {
      return null;
    }
    _currentFlowLabel = root.Q<Label>(CurrentFlowLabelName);
    return _currentFlowLabel;
  }

  static float GetValveFlow(ThrottlingValve throttlingValve) {
    var coordinates = throttlingValve._blockObject.Coordinates;
    if (!_threadSafeWaterMap.TryGetColumnFloor(coordinates, out var floor)) {
      return 0f;
    }

    var flow = _threadSafeWaterMap.WaterFlowDirection(new Vector3Int(coordinates.x, coordinates.y, floor));
    var directionalFlow = throttlingValve._blockObject.Orientation.ToFlowDirection() switch {
        FlowDirection.Top => flow.y,
        FlowDirection.Right => flow.x,
        FlowDirection.Bottom => -flow.y,
        FlowDirection.Left => -flow.x,
        _ => flow.magnitude,
    };
    return Numbers.RoundToPrecision(Mathf.Max(0f, directionalFlow), 0.01f);
  }
}

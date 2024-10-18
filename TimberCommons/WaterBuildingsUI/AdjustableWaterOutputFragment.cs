// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.Settings;
using IgorZ.TimberCommons.WaterBuildings;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

sealed class AdjustableWaterOutputFragment : IEntityPanelFragment {
  const string StopAboveDownstreamDepthLocKey = "IgorZ.TimberCommons.AdjustableWaterOutput.StopAboveDownstream";
  const float WaterLevelChangeStep = 0.05f;

  readonly UiFactory _uiFactory;
  
  VisualElement _root;
  Label _infoLabel;
  PreciseSliderWrapper _waterLevelSlider;
  AdjustableWaterOutput _adjustableWaterOutput;

  int Range => _adjustableWaterOutput.MaxHeight - _adjustableWaterOutput.MinHeight;
  float WaterLevelSliderValue => Range + _adjustableWaterOutput.SpillwayHeightDelta;

  AdjustableWaterOutputFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  /// <inheritdoc/>
  public VisualElement InitializeFragment() {
    _waterLevelSlider = _uiFactory.CreatePreciseSlider(
        WaterLevelChangeStep, v => _adjustableWaterOutput.SetSpillwayHeightDelta(v - Range));
    _infoLabel = _uiFactory.CreateLabel();
    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_infoLabel)
        .AddComponent(_waterLevelSlider)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  /// <inheritdoc/>
  public void ShowFragment(BaseComponent entity) {
    _adjustableWaterOutput = entity.GetComponentFast<AdjustableWaterOutput>();
    if (!_adjustableWaterOutput || !_adjustableWaterOutput.AllowAdjustmentsInGame) {
      _adjustableWaterOutput = null;
      return;
    }
    _root.ToggleDisplayStyle(visible: true);
    _waterLevelSlider.UpdateValuesWithoutNotify(WaterLevelSliderValue, Range);
  }

  /// <inheritdoc/>
  public void ClearFragment() {
    _adjustableWaterOutput = null;
    _root.ToggleDisplayStyle(visible: false);
  }

  /// <inheritdoc/>
  public void UpdateFragment() {
    if (!_adjustableWaterOutput) {
      return;
    }
    _infoLabel.text = _uiFactory.Loc.T(StopAboveDownstreamDepthLocKey, _waterLevelSlider.Value.ToString("0.00"));
    _waterLevelSlider.UpdateValuesWithoutNotify(WaterLevelSliderValue, Range);
  }
}

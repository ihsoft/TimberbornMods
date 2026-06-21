// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.WaterBuildings;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

sealed class AdjustableWaterOutputFragment : IEntityPanelFragment {
  const string LimitOutputLevelLocKey = "IgorZ.TimberCommons.AdjustableWaterOutput.LimitOutputLevel";
  const string StopAboveDownstreamDepthLocKey = "IgorZ.TimberCommons.AdjustableWaterOutput.StopAboveDownstream";
  const float WaterLevelChangeStep = 0.05f;

  readonly UiFactory _uiFactory;
  
  VisualElement _root;
  Toggle _limitOutputLevelToggle;
  Label _infoLabel;
  PreciseSliderWrapper _waterLevelSlider;
  AdjustableWaterOutput _adjustableWaterOutput;

  int Range => _adjustableWaterOutput.MaxSliderTargetHeight - _adjustableWaterOutput.MinHeight;
  float WaterLevelSliderValue => _adjustableWaterOutput.TargetWaterLevel - _adjustableWaterOutput.MinHeight;

  AdjustableWaterOutputFragment(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  /// <inheritdoc/>
  public VisualElement InitializeFragment() {
    //FIXME: create an asset
    _waterLevelSlider = _uiFactory.CreatePreciseSlider(
        WaterLevelChangeStep,
        v => _adjustableWaterOutput.SetTargetWaterLevel(_adjustableWaterOutput.MinHeight + v));
    _limitOutputLevelToggle = _uiFactory.CreateToggle(
        LimitOutputLevelLocKey, e => {
          _adjustableWaterOutput.SetLimitOutputLevelEnabled(e.newValue);
          _adjustableWaterOutput.GetComponent<AdjustableWaterOutputMarker>()?.RefreshVisibility();
          UpdateControls();
        });
    _infoLabel = _uiFactory.CreateLabel();
    _root = _uiFactory.CreateCenteredPanelFragment();
    _root.Add(_limitOutputLevelToggle);
    _root.Add(_infoLabel);
    _root.Add(_waterLevelSlider);
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  /// <inheritdoc/>
  public void ShowFragment(BaseComponent entity) {
    _adjustableWaterOutput = entity.GetComponent<AdjustableWaterOutput>();
    if (!_adjustableWaterOutput || !_adjustableWaterOutput.AllowAdjustmentsInGame) {
      _adjustableWaterOutput = null;
      return;
    }
    _root.ToggleDisplayStyle(visible: true);
    UpdateControls();
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
    UpdateControls();
  }

  void UpdateControls() {
    if (!_adjustableWaterOutput) {
      return;
    }
    var limitEnabled = _adjustableWaterOutput.LimitOutputLevelEnabled;
    _limitOutputLevelToggle.SetValueWithoutNotify(limitEnabled);
    _infoLabel.ToggleDisplayStyle(visible: limitEnabled);
    _waterLevelSlider.ToggleDisplayStyle(visible: limitEnabled);
    if (!limitEnabled) {
      return;
    }
    _waterLevelSlider.UpdateValuesWithoutNotify(WaterLevelSliderValue, Range);
    _infoLabel.text = _uiFactory.T(StopAboveDownstreamDepthLocKey, _waterLevelSlider.Value.ToString("0.00"));
  }
}

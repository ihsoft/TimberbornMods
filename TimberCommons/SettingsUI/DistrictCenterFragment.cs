// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.Settings;
using IgorZ.TimberDev.UI;
using ModSettings.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.Debugging;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.SettingsUI;

sealed class DistrictCenterFragment : IEntityPanelFragment {
  readonly DevModeManager _devModeManager;
  readonly UiFactory _uiFactory;
  readonly TimeAndDurationSettings _timeAndDurationSettings;
  readonly IrrigationSystemSettings _irrigationSystemSettings;
  readonly WaterBuildingsSettings _waterBuildingsSettings;
  readonly InjuryProbabilitySettings _injuryProbabilitySettings;
  readonly DebugSettings _debugSettings;

  DistrictCenter _districtCenter;
  VisualElement _root;
  Toggle _overrideDesertLevelsForWaterTowersToggle;
  Toggle _daysHoursSupplyLeftToggle;
  Toggle _daysHoursGrowingTimeToggle;
  Toggle _daysHoursForSlowRecipesToggle;
  Toggle _higherPrecisionForFuelConsumingRecipesToggle;
  Toggle _adjustWaterDepthAtSpillwayOnMechanicalPumpsToggle;
  Toggle _adjustWaterDepthAtSpillwayOnFluidDumpsToggle;
  Toggle _showCurrentStrengthInSluiceToggle;
  Toggle _showInFragmentToggle;
  Toggle _showAvatarHintToggle;
  Toggle _verboseLoggingToggle;

  DistrictCenterFragment(DevModeManager devModeManager,
                         UiFactory uiFactory,
                         TimeAndDurationSettings timeAndDurationSettings,
                         IrrigationSystemSettings irrigationSystemSettings,
                         WaterBuildingsSettings waterBuildingsSettings,
                         InjuryProbabilitySettings injuryProbabilitySettings,
                         DebugSettings debugSettings) {
    _devModeManager = devModeManager;
    _uiFactory = uiFactory;
    _timeAndDurationSettings = timeAndDurationSettings;
    _irrigationSystemSettings = irrigationSystemSettings;
    _waterBuildingsSettings = waterBuildingsSettings;
    _injuryProbabilitySettings = injuryProbabilitySettings;
    _debugSettings = debugSettings;
  }

  /// <inheritdoc/>
  public VisualElement InitializeFragment() {
    _overrideDesertLevelsForWaterTowersToggle = MakeSettingToggle(
        "OverrideDesertLevelsForWaterTowers", _irrigationSystemSettings._overrideDesertLevelsForWaterTowers);
    _daysHoursSupplyLeftToggle = MakeSettingToggle(
        "DaysHoursSupplyLeft", _timeAndDurationSettings._daysHoursSupplyLeft);
    _daysHoursGrowingTimeToggle = MakeSettingToggle(
        "DaysHoursGrowingTime", _timeAndDurationSettings._daysHoursGrowingTime);
    _daysHoursForSlowRecipesToggle = MakeSettingToggle(
        "DaysHoursForSlowRecipes", _timeAndDurationSettings._daysHoursForSlowRecipes);
    _higherPrecisionForFuelConsumingRecipesToggle = MakeSettingToggle(
        "HigherPrecisionForFuelConsumingRecipes", _timeAndDurationSettings._higherPrecisionForFuelConsumingRecipes);
    _adjustWaterDepthAtSpillwayOnMechanicalPumpsToggle = MakeSettingToggle(
        "AdjustWaterDepthAtSpillwayOnMechanicalPumps",
        _waterBuildingsSettings._adjustWaterDepthAtSpillwayOnMechanicalPumps);
    _adjustWaterDepthAtSpillwayOnFluidDumpsToggle = MakeSettingToggle(
        "AdjustWaterDepthAtSpillwayOnFluidDumps", _waterBuildingsSettings._adjustWaterDepthAtSpillwayOnFluidDumps);
    _showCurrentStrengthInSluiceToggle = MakeSettingToggle(
        "ShowCurrentStrengthInSluice", _waterBuildingsSettings._showCurrentStrengthInSluice);
    _showInFragmentToggle = MakeSettingToggle("ShowInFragment", _injuryProbabilitySettings.ShowInFragment);
    _showAvatarHintToggle = MakeSettingToggle("ShowAvatarHint", _injuryProbabilitySettings.ShowAvatarHint);
    _verboseLoggingToggle = MakeSettingToggle("VerboseLogging", _debugSettings._verboseLogging);

    _root = _uiFactory.CreateCenteredPanelFragmentBuilder()
        .AddComponent(_overrideDesertLevelsForWaterTowersToggle)
        .AddComponent(_daysHoursSupplyLeftToggle)
        .AddComponent(_daysHoursGrowingTimeToggle)
        .AddComponent(_daysHoursForSlowRecipesToggle)
        .AddComponent(_higherPrecisionForFuelConsumingRecipesToggle)
        .AddComponent(_adjustWaterDepthAtSpillwayOnMechanicalPumpsToggle)
        .AddComponent(_adjustWaterDepthAtSpillwayOnFluidDumpsToggle)
        .AddComponent(_showCurrentStrengthInSluiceToggle)
        .AddComponent(_showInFragmentToggle)
        .AddComponent(_showAvatarHintToggle)
        .AddComponent(_verboseLoggingToggle)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  /// <inheritdoc/>
  public void ShowFragment(BaseComponent entity) {
    _districtCenter = entity.GetComponentFast<DistrictCenter>();
  }

  /// <inheritdoc/>
  public void ClearFragment() {
    _districtCenter = null;
    UpdateFragment();
  }

  /// <inheritdoc/>
  public void UpdateFragment() {
    _root.ToggleDisplayStyle(_districtCenter && _districtCenter.enabled && _devModeManager.Enabled);
  }

  Toggle MakeSettingToggle(string name, ModSetting<bool> setting) {
    var toggle = _uiFactory.CreateToggle(name, e => setting.SetValue(e.newValue));
    toggle.SetValueWithoutNotify(setting.Value);
    return toggle;
  } 
}

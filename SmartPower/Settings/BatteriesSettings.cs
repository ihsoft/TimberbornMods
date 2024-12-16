// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class BatteriesSettings : ModSettingsOwner {
  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ShowBatteryVitals => _instance._showBatteryVitals.Value;
  public ModSetting<bool> _showBatteryVitals { get; } = new(
      true,
      ModSettingDescriptor.CreateLocalized("IgorZ.SmartPower.Settings.Batteries.ShowBatteryVitals"));

  public static float BatteryRatioHysteresis => _instance._batteryRatioHysteresis.Value;
  public ModSetting<int> _batteryRatioHysteresis { get; } = new RangeIntModSetting(
      1, 0, 5,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.SmartPower.Settings.Batteries.BatteryRatioHysteresis")
          .SetLocalizedTooltip("IgorZ.SmartPower.Settings.Batteries.BatteryRatioHysteresisTooltip"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.SmartPower.Settings.BatteriesSection";

  /// <inheritdoc />
  public override int Order => 1;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  static BatteriesSettings _instance;

  public BatteriesSettings(
    ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
  }

  #endregion
}

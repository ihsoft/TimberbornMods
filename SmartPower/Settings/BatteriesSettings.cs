// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class BatteriesSettings : BaseSettings<BatteriesSettings> {

  const string HeaderStringLocKey = "IgorZ.SmartPower.Settings.BatteriesSection";
  const string ShowBatteryVitalsLocKey = "IgorZ.SmartPower.Settings.Batteries.ShowBatteryVitals";
  const string BatteryCapacityAsPctLocKey = "IgorZ.SmartPower.Settings.Batteries.BatteryCapacityAsPct";
  const string BatteryRatioHysteresisLocKey = "IgorZ.SmartPower.Settings.Batteries.BatteryRatioHysteresis";
  const string BatteryRatioHysteresisTooltipLocKey = "IgorZ.SmartPower.Settings.Batteries.BatteryRatioHysteresisTooltip";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ShowBatteryVitals { get; private set; }
  public ModSetting<bool> ShowBatteryVitalsInternal { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(ShowBatteryVitalsLocKey));

  public static bool BatteryCapacityAsPct { get; private set; }
  public ModSetting<bool> BatteryCapacityAsPctInternal { get; } =
      new(true,
          ModSettingDescriptor
              .CreateLocalized(BatteryCapacityAsPctLocKey)
              .SetEnableCondition(() => ShowBatteryVitals));

  public static float BatteryRatioHysteresis { get; private set; }
  public ModSetting<int> BatteryRatioHysteresisInternal { get; } =
      new RangeIntModSetting(
          1, 0, 5,
          ModSettingDescriptor
              .CreateLocalized(BatteryRatioHysteresisLocKey)
              .SetLocalizedTooltip(BatteryRatioHysteresisTooltipLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 1;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  BatteriesSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(ShowBatteryVitalsInternal, v => ShowBatteryVitals = v);
    InstallSettingCallback(BatteryCapacityAsPctInternal, v => BatteryCapacityAsPct = v);
    InstallSettingCallback(BatteryRatioHysteresisInternal, v => BatteryRatioHysteresis = v);
  }

  #endregion
}

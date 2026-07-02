// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class TimeAndDurationSettings : BaseSettings<TimeAndDurationSettings> {
  const string HeaderStringLocKey = "IgorZ.TimberCommons.Settings.TimeAndDurationSection";
  const string DaysHoursSupplyLeftLocKey = "IgorZ.TimberCommons.Settings.TimeAndDuration.DaysHoursSupplyLeft";
  const string DaysHoursGrowingTimeLocKey = "IgorZ.TimberCommons.Settings.TimeAndDuration.DaysHoursGrowingTime";
  const string DaysHoursForRecipeDurationLocKey =
      "IgorZ.TimberCommons.Settings.TimeAndDuration.DaysHoursForRecipeDuration";
  const string HigherPrecisionForFuelConsumingRecipesLocKey =
      "IgorZ.TimberCommons.Settings.TimeAndDuration.HigherPrecisionForFuelConsumingRecipes";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global

  public static bool DaysHoursSupplyLeft { get; private set; } = true;
  public ModSetting<bool> DaysHoursSupplyLeftInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(DaysHoursSupplyLeftLocKey));

  public static bool DaysHoursGrowingTime { get; private set; } = true;
  public ModSetting<bool> DaysHoursGrowingTimeInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(DaysHoursGrowingTimeLocKey));

  public static bool DaysHoursForRecipeDuration { get; private set; } = true;
  public ModSetting<bool> DaysHoursForRecipeDurationInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(DaysHoursForRecipeDurationLocKey));

  public static bool HigherPrecisionForFuelConsumingRecipes { get; private set; } = true;
  public ModSetting<bool> HigherPrecisionForFuelConsumingRecipesInternal { get; } = new(
      true, ModSettingDescriptor.CreateLocalized(HigherPrecisionForFuelConsumingRecipesLocKey));

  // ReSharper restore MemberCanBePrivate.Global
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

  TimeAndDurationSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(DaysHoursSupplyLeftInternal, v => DaysHoursSupplyLeft = v);
    InstallSettingCallback(DaysHoursGrowingTimeInternal, v => DaysHoursGrowingTime = v);
    InstallSettingCallback(DaysHoursForRecipeDurationInternal, v => DaysHoursForRecipeDuration = v);
    InstallSettingCallback(
        HigherPrecisionForFuelConsumingRecipesInternal, v => HigherPrecisionForFuelConsumingRecipes = v);
  }

  #endregion
}

﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class TimeAndDurationSettings : ModSettingsOwner {

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool DaysHoursSupplyLeft => _instance._daysHoursSupplyLeft.Value;
  public ModSetting<bool> _daysHoursSupplyLeft { get; } = new(
      true, ModSettingDescriptor.CreateLocalized("IgorZ.TimberCommons.Settings.TimeAndDuration.DaysHoursSupplyLeft"));

  public static bool DaysHoursGrowingTime => _instance._daysHoursGrowingTime.Value;
  public ModSetting<bool> _daysHoursGrowingTime { get; } = new(
      true, ModSettingDescriptor.CreateLocalized("IgorZ.TimberCommons.Settings.TimeAndDuration.DaysHoursGrowingTime"));

  public static bool DaysHoursForRecipeDuration => _instance._daysHoursForSlowRecipes.Value;
  public ModSetting<bool> _daysHoursForSlowRecipes { get; } = new(
      true,
      ModSettingDescriptor.CreateLocalized("IgorZ.TimberCommons.Settings.TimeAndDuration.DaysHoursForRecipeDuration"));

  public static bool HigherPrecisionForFuelConsumingRecipes => _instance._higherPrecisionForFuelConsumingRecipes.Value;
  public ModSetting<bool> _higherPrecisionForFuelConsumingRecipes { get; } = new(
      true,
      ModSettingDescriptor.CreateLocalized(
          "IgorZ.TimberCommons.Settings.TimeAndDuration.HigherPrecisionForFuelConsumingRecipes"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.TimberCommons.Settings.TimeAndDurationSection";

  /// <inheritdoc />
  public override int Order => 1;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  static TimeAndDurationSettings _instance;

  TimeAndDurationSettings(
          ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
          : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
  }

  #endregion
}

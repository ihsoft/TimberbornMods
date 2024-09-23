// Timberborn Mod: Timberborn Commons
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
  public ModSetting<bool> _daysHoursSupplyLeft { get; } = 
      new(true, ModSettingDescriptor.Create("Show \"supply left\" as days/hours"));

  public static bool DaysHoursGrowingTime => _instance._daysHoursGrowingTime.Value;
  public ModSetting<bool> _daysHoursGrowingTime { get; } = 
      new(true, ModSettingDescriptor.Create("Show grow time as days/hours"));

  public static bool DaysHoursForRecipeDuration => _instance._daysHoursForSlowRecipes.Value;
  public ModSetting<bool> _daysHoursForSlowRecipes { get; } = 
      new(true, ModSettingDescriptor.Create("Show recipes time as days/hours"));

  public static bool HigherPrecisionForFuelConsumingRecipes => _instance._higherPrecisionForFuelConsumingRecipes.Value;
  public ModSetting<bool> _higherPrecisionForFuelConsumingRecipes { get; } = 
      new(true, ModSettingDescriptor.Create("Show recipe fuel consumption with a higher precision"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => "Timberborn.IgorZ.TimberCommons";

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.TimberCommons.Settings.TimeAndDurationSection";

  /// <inheritdoc />
  public override int Order => 1;

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

// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class IrrigationSystemSettings : ModSettingsOwner {
  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool OverrideDesertLevelsForWaterTowers => _instance._overrideDesertLevelsForWaterTowers.Value;
  public ModSetting<bool> _overrideDesertLevelsForWaterTowers { get; } = new(
    true,
    ModSettingDescriptor
        .CreateLocalized("IgorZ.TimberCommons.Settings.IrrigationSystem.OverrideDesertLevelsForWaterTowers")
        .SetLocalizedTooltip(
            "IgorZ.TimberCommons.Settings.IrrigationSystem.OverrideDesertLevelsForWaterTowersTooltip"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.TimberCommons.Settings.IrrigationSystemSection";

  /// <inheritdoc />
  public override int Order => 2;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  static IrrigationSystemSettings _instance;

  public IrrigationSystemSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
  }

  #endregion
}

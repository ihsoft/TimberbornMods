// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class IrrigationSystemSettings : BaseSettings<IrrigationSystemSettings> {
  const string HeaderStringLocKey = "IgorZ.TimberCommons.Settings.IrrigationSystemSection";
  const string OverrideDesertLevelsForWaterTowersLocKey =
      "IgorZ.TimberCommons.Settings.IrrigationSystem.OverrideDesertLevelsForWaterTowers";
  const string OverrideDesertLevelsForWaterTowersTooltipLocKey =
      "IgorZ.TimberCommons.Settings.IrrigationSystem.OverrideDesertLevelsForWaterTowersTooltip";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable MemberCanBePrivate.Global

  public static bool OverrideDesertLevelsForWaterTowers { get; private set; } = true;
  public ModSetting<bool> OverrideDesertLevelsForWaterTowersInternal { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized(OverrideDesertLevelsForWaterTowersLocKey)
          .SetLocalizedTooltip(OverrideDesertLevelsForWaterTowersTooltipLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 2;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  public IrrigationSystemSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(OverrideDesertLevelsForWaterTowersInternal, v => OverrideDesertLevelsForWaterTowers = v);
  }

  #endregion
}

// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class ManufactorySettings : BaseSettings<ManufactorySettings> {

  const string HeaderStringLocKey = "IgorZ.SmartPower.Settings.ManufactorySection";
  const string ConsumeOneHorsepowerLocKey = "IgorZ.SmartPower.Settings.Manufactory.ConsumeOneHorsepower";
  const string ConsumeOneHorsepowerTooltipLocKey =
      "IgorZ.SmartPower.Settings.Manufactory.ConsumeOneHorsepowerTooltip";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ConsumeOneHorsepowerInPowerSavingMode { get; private set; }
  public ModSetting<bool> ConsumeOneHorsepowerInPowerSavingModeInternal { get; } =
      new(false, ModSettingDescriptor.CreateLocalized(ConsumeOneHorsepowerLocKey)
          .SetLocalizedTooltip(ConsumeOneHorsepowerTooltipLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 7;

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  ManufactorySettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(
        ConsumeOneHorsepowerInPowerSavingModeInternal, v => ConsumeOneHorsepowerInPowerSavingMode = v);
  }
}

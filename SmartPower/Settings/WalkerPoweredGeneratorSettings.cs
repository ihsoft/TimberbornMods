// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Common;
using ModSettings.Core;
using TimberApi;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class WalkerPoweredGeneratorSettings(
    ISettings settings,
    ModSettingsOwnerRegistry modSettingsOwnerRegistry,
    ModRepository modRepository) : ModSettingsOwner(settings, modSettingsOwnerRegistry, modRepository) {

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public ModSetting<bool> ShowFloatingIcon { get; } = new(
      true,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.SmartPower.Settings.UI.ShowFloatingIcon")
          .SetEnableCondition(IsMenuMode));

  public ModSetting<int> SuspendDelayMinutes { get; } = new RangeIntModSetting(
      30, 0, 120,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.SmartPower.Settings.Hysteresis.SuspendDelay")
          .SetEnableCondition(IsMenuMode));

  public ModSetting<int> ResumeDelayMinutes { get; } = new RangeIntModSetting(
      15, 0, 120,
      ModSettingDescriptor
          .CreateLocalized("IgorZ.SmartPower.Settings.Hysteresis.ResumeDelay")
          .SetEnableCondition(IsMenuMode));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.SmartPower.Settings.WalkerPoweredGeneratorSection";

  /// <inheritdoc />
  public override int Order => 2;

  #endregion

  #region Implementation

  static bool IsMenuMode() => SceneManager.CurrentScene == Scene.MainMenu;

  #endregion
}

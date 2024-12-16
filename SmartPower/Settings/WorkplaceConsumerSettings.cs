// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class WorkplaceConsumerSettings(
    ISettings settings,
    ModSettingsOwnerRegistry modSettingsOwnerRegistry,
    ModRepository modRepository) : ModSettingsOwner(settings, modSettingsOwnerRegistry, modRepository) {

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public ModSetting<bool> ShowFloatingIcon { get; } = new(
      true,
      ModSettingDescriptor.CreateLocalized("IgorZ.SmartPower.Settings.UI.ShowFloatingIcon"));

  public ModSetting<int> SuspendDelayMinutes { get; } = new RangeIntModSetting(
      60, 0, 120,
      ModSettingDescriptor.CreateLocalized("IgorZ.SmartPower.Settings.Hysteresis.SuspendDelay"));

  public ModSetting<int> ResumeDelayMinutes { get; } = new RangeIntModSetting(
      60, 0, 120,
      ModSettingDescriptor.CreateLocalized("IgorZ.SmartPower.Settings.Hysteresis.ResumeDelay"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.SmartPower.Settings.WorkplaceConsumerSection";

  /// <inheritdoc />
  public override int Order => 4;

  #endregion
}

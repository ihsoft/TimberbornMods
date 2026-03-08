// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class UnmannedConsumerSettings : BaseSettings<UnmannedConsumerSettings> {

  const string HeaderStringLocKey = "IgorZ.SmartPower.Settings.UnmannedConsumerSection";
  const string ShowFloatingIconLocKey = "IgorZ.SmartPower.Settings.UI.ShowFloatingIcon";

  protected override string ModId => Configurator.ModId;

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ShowFloatingIcon { get; private set; }
  public ModSetting<bool> ShowFloatingIconInternal { get; } =
      new(true, ModSettingDescriptor.CreateLocalized(ShowFloatingIconLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 5;

  #endregion

  UnmannedConsumerSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    InstallSettingCallback(ShowFloatingIconInternal, v => ShowFloatingIcon = v);
  }
}

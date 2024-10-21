// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.SmartPower.Settings;

sealed class NetworkUISettings : ModSettingsOwner {
  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public static bool ShowBatteryVitals => _instance._showBatteryVitals.Value;
  public ModSetting<bool> _showBatteryVitals { get; } = new(
    true,
    ModSettingDescriptor.CreateLocalized("IgorZ.SmartPower.Settings.NetworkUI.ShowBatteryVitals"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.SmartPower.Settings.NetworkUISection";

  /// <inheritdoc />
  public override int Order => 1;

  #endregion

  #region Implementation

  static NetworkUISettings _instance;

  public NetworkUISettings(
    ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _instance = this;
  }

  #endregion
}

// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartPower.Settings;

sealed class DebugSettings : ModSettingsOwner {
  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public ModSetting<bool> _verboseLogging { get; } = new(
    false,
    ModSettingDescriptor
        .CreateLocalized("IgorZ.SmartPower.Settings.Debug.VerboseLogging")
        .SetLocalizedTooltip("IgorZ.SmartPower.Settings.Debug.VerboseLoggingTooltip"));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  protected override string ModId => Configurator.ModId;

  /// <inheritdoc />
  public override string HeaderLocKey => "IgorZ.SmartPower.Settings.DebugSection";

  /// <inheritdoc />
  public override int Order => 100;  // Always last.

  #endregion

  #region Implementation

  public DebugSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _verboseLogging.ValueChanged += (_, value) => UpdateVerbosityLevel();
  }

  protected override void OnAfterLoad() {
    UpdateVerbosityLevel();
  }

  void UpdateVerbosityLevel() {
    DebugEx.LoggingSettings.VerbosityLevel = _verboseLogging.Value ? 5 : 0;
  }

  #endregion
}

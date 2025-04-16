// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Settings;

/// <summary>Base class for debug mod settings.</summary>
/// <remarks>
/// By default, it only controls the log verbosity level, but more settings can be introduced by adding more
/// <see cref="ModSetting"/> properties in the descendants. See "DebugSettingsConfigurator.cs" for an example of binding
/// this class.
/// </remarks>
abstract class DebugSettings : ModSettingsOwner {
  const string VerboseLoggingLocKey = "TimberDev_Utils.Settings.Debug.VerboseLogging";
  const string VerboseLoggingTooltipLocKey = "TimberDev_Utils.Settings.Debug.VerboseLoggingTooltip";
  const string HeaderStringLocKey = "TimberDev_Utils.Settings.DebugSection";

  #region Settings
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  public ModSetting<bool> _verboseLogging { get; } = new(
      false,
      ModSettingDescriptor.CreateLocalized(VerboseLoggingLocKey).SetLocalizedTooltip(VerboseLoggingTooltipLocKey));

  // ReSharper restore MemberCanBePrivate.Global
  // ReSharper restore InconsistentNaming
  #endregion

  #region ModSettingsOwner overrides

  /// <inheritdoc />
  public override string HeaderLocKey => HeaderStringLocKey;

  /// <inheritdoc />
  public override int Order => 100;  // Always last.

  /// <inheritdoc />
  public override ModSettingsContext ChangeableOn => ModSettingsContext.MainMenu | ModSettingsContext.Game;

  #endregion

  #region Implementation

  protected DebugSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
    _verboseLogging.ValueChanged += (_, _) => UpdateVerbosityLevel();
  }

  protected override void OnAfterLoad() {
    UpdateVerbosityLevel();
  }

  void UpdateVerbosityLevel() {
    var oldLevel = DebugEx.VerbosityLevel;
    DebugEx.VerbosityLevel = _verboseLogging.Value ? DebugEx.LogLevel.Finer : DebugEx.LogLevel.Info;
    if (oldLevel != DebugEx.VerbosityLevel) {
      DebugEx.Info("Debug verbosity level changed: from={0}, to={1}", oldLevel, DebugEx.VerbosityLevel);
    }
  }

  #endregion
}

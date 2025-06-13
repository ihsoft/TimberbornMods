// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
abstract class DebugSettings<T> : BaseSettings<T> where T : DebugSettings<T> {
  const string VerboseLoggingLocKey = "TimberDev_Utils.Settings.Debug.VerboseLogging";
  const string VerboseLoggingTooltipLocKey = "TimberDev_Utils.Settings.Debug.VerboseLoggingTooltip";
  const string HeaderStringLocKey = "TimberDev_Utils.Settings.DebugSection";

  #region Settings

  public ModSetting<bool> VerboseLogging { get; } = new(
      false,
      ModSettingDescriptor.CreateLocalized(VerboseLoggingLocKey).SetLocalizedTooltip(VerboseLoggingTooltipLocKey));

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
    InstallSettingCallback(VerboseLogging, UpdateVerbosityLevel);
  }

  void UpdateVerbosityLevel() {
    var oldLevel = DebugEx.VerbosityLevel;
    DebugEx.VerbosityLevel = VerboseLogging.Value ? DebugEx.LogLevel.Finer : DebugEx.LogLevel.Info;
    if (oldLevel != DebugEx.VerbosityLevel) {
      DebugEx.Info("Debug verbosity level changed for {0}: from={1}, to={2}", ModId, oldLevel, DebugEx.VerbosityLevel);
    }
  }

  #endregion
}

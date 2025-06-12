// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.Automation.Settings;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
sealed class AutomationDebugSettings : DebugSettings {

  const string PathCheckingProfilingLocKey = "IgorZ.Automation.Settings.Debug.PathCheckingProfiling";
  const string LogSignalSettingLocKey = "IgorZ.Automation.Settings.Debug.LogSignalSetting";
  const string LogSignalPropagatingKey = "IgorZ.Automation.Settings.Debug.LogSignalPropagating";

  protected override string ModId => Configurator.AutomationModId;

  // ReSharper disable once UnusedMember.Local
  public ModSetting<bool> PathCheckingSystemProfilingInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(PathCheckingProfilingLocKey));
  public static bool PathCheckingSystemProfiling { get; private set; }

  public ModSetting<bool> LogSignalsSettingInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(LogSignalSettingLocKey));
    public static bool LogSignalsSetting => _instance.LogSignalsSettingInternal.Value;

  public ModSetting<bool> LogSignalsPropagatingInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(LogSignalPropagatingKey));
    public static bool LogSignalsPropagating => _instance.LogSignalsPropagatingInternal.Value;

  static AutomationDebugSettings _instance;

  AutomationDebugSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) { 
    _instance = this;
    PathCheckingSystemProfilingInternal.ValueChanged +=
        (_, _) => PathCheckingSystemProfiling = PathCheckingSystemProfilingInternal.Value;
  }
}

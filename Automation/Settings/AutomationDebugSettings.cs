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
sealed class AutomationDebugSettings : DebugSettings<AutomationDebugSettings> {

  const string PathCheckingProfilingLocKey = "IgorZ.Automation.Settings.Debug.PathCheckingProfiling";
  const string LogSignalSettingLocKey = "IgorZ.Automation.Settings.Debug.LogSignalSetting";
  const string LogSignalPropagatingKey = "IgorZ.Automation.Settings.Debug.LogSignalPropagating";
  const string ReevaluateRulesOnLoadKey = "IgorZ.Automation.Settings.Debug.ReevaluateRulesOnLoad";
  const string ResetSignalsOnLoadKey = "IgorZ.Automation.Settings.Debug.ResetSignalsOnLoad";

  protected override string ModId => Configurator.AutomationModId;

  public ModSetting<bool> PathCheckingSystemProfilingInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(PathCheckingProfilingLocKey));
  public static bool PathCheckingSystemProfiling { get; private set; }

  public ModSetting<bool> LogSignalsSettingInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(LogSignalSettingLocKey));
  public static bool LogSignalsSetting => Instance.VerboseLogging.Value && Instance.LogSignalsSettingInternal.Value;

  public ModSetting<bool> LogSignalsPropagatingInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(LogSignalPropagatingKey));
  public static bool LogSignalsPropagating =>
      Instance.VerboseLogging.Value && Instance.LogSignalsPropagatingInternal.Value;

  public ModSetting<bool> ReevaluateRulesOnLoadInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(ReevaluateRulesOnLoadKey));
  public static bool ReevaluateRulesOnLoad => Instance.ReevaluateRulesOnLoadInternal.Value;

  public ModSetting<bool> ResetSignalsOnLoadInternal { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(ResetSignalsOnLoadKey));
  public static bool ResetSignalsOnLoad => ReevaluateRulesOnLoad && Instance.ResetSignalsOnLoadInternal.Value;

  AutomationDebugSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) { 
    InstallSettingCallback(PathCheckingSystemProfilingInternal, v => PathCheckingSystemProfiling = v);
    LogSignalsSettingInternal.Descriptor.SetEnableCondition(() => VerboseLogging.Value);
    LogSignalsPropagatingInternal.Descriptor.SetEnableCondition(() => VerboseLogging.Value);
    ResetSignalsOnLoadInternal.Descriptor.SetEnableCondition(() => ReevaluateRulesOnLoadInternal.Value);
  }
}

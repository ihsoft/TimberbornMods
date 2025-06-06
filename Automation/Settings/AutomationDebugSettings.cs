﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.Automation.Settings;

sealed class AutomationDebugSettings : DebugSettings {

  const string PathCheckingProfilingLocKey = "IgorZ.Automation.Settings.Debug.PathCheckingProfiling";
  const string LogSignalSettingLocKey = "IgorZ.Automation.Settings.Debug.LogSignalSetting";
  const string LogSignalPropagatingKey = "IgorZ.Automation.Settings.Debug.LogSignalPropagating";

  protected override string ModId => Configurator.AutomationModId;

  // ReSharper disable once UnusedMember.Local
  public ModSetting<bool> PathCheckingSystemProfiling { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(PathCheckingProfilingLocKey));

  public ModSetting<bool> LogSignalsSetting { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(LogSignalSettingLocKey));

  public ModSetting<bool> LogSignalsPropagating { get; } = new(
      false, ModSettingDescriptor.CreateLocalized(LogSignalPropagatingKey));

  AutomationDebugSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) {
      
    LogSignalsSetting.Descriptor.SetEnableCondition(() => _verboseLogging.Value);
    LogSignalsPropagating.Descriptor.SetEnableCondition(() => _verboseLogging.Value);
  }
}

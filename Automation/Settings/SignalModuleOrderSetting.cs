// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using ModSettings.Core;

namespace IgorZ.Automation.Settings;

/// <summary>Marker setting rendered by the custom signal-module-order UI.</summary>
sealed class SignalModuleOrderSetting(string defaultValue, ModSettingDescriptor descriptor)
    : ModSetting<string>(defaultValue, descriptor);

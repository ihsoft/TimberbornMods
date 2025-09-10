// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace IgorZ.TimberCommons.Settings;

sealed class TimberCommonsDebugSettings : DebugSettings<TimberCommonsDebugSettings> {
  protected override string ModId => Configurator.ModId;

  TimberCommonsDebugSettings(
      ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
      : base(settings, modSettingsOwnerRegistry, modRepository) { }
}

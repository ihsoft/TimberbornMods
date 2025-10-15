// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;
  static readonly Type[] Patches = [
      typeof(ForesterSetReplantDeadTreesPatch),
      typeof(SoilContaminationServicePatch),
      typeof(SoilMoistureServicePatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(PatchId, Patches);
  }
}

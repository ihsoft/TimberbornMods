// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Patches;

// ReSharper disable once UnusedType.Global
[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, typeof(FloodgatePatch), typeof(NoUnemployedStatusPatch));
  }
}

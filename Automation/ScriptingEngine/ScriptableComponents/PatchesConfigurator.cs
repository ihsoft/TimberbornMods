// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

// ReSharper disable once UnusedType.Global
[Context("MainMenu")]
sealed class PatchesConfigurator : IConfigurator {
  static readonly string PatchId = typeof(PatchesConfigurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(PatchId, typeof(FloodgateSetHeightPatch));
  }
}

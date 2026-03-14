// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.CustomTools.KeyBindings;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string HarmonyPatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<KeyBindingInputProcessor>().AsSingleton();
    HarmonyPatcher.ApplyPatch(HarmonyPatchId, typeof(KeyBindingPatch));
  }
}

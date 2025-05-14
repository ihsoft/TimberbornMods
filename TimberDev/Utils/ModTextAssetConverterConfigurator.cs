// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

[Context("MainMenu")]
class ModTextAssetConverterConfigurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(typeof(Configurator).AssemblyQualifiedName, typeof(ModTextAssetConverterPatch));
  }
}

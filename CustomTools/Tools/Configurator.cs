// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.CustomTools.Tools;

[Context("Game")]
sealed class Configurator : IConfigurator {
  static readonly string HarmonyPatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<DebugFinishNowTool>().AsSingleton();
    containerDefinition.Bind<PauseTool>().AsSingleton();
    containerDefinition.Bind<ResumeTool>().AsSingleton();
    containerDefinition.Bind<FourTemplatesBlockObjectTool>().AsTransient();
    HarmonyPatcher.ApplyPatch(HarmonyPatchId, typeof(BlockObjectToolWarningPanelPatch));
  }
}

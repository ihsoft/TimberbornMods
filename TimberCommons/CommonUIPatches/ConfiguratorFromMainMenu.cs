// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.CommonUIPatches;

[Context("MainMenu")]
sealed class ConfiguratorFromMainMenu : IConfigurator {
  static readonly string PatchId = typeof(ConfiguratorFromMainMenu).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(ModListViewInitializePatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
  }
}

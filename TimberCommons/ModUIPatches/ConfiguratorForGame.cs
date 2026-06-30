// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.ModUIPatches;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class ConfiguratorForGame : IConfigurator {
  static readonly string PatchId = typeof(ConfiguratorForGame).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(ModListViewInitializePatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
    containerDefinition.Bind<ModListViewLocInitializer>().AsSingleton();
  }
}
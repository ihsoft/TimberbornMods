// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.ModUIPatches;

[Context("MainMenu")]
[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(ModListViewInitializePatch),
      typeof(ModManagerBoxOpenPatch),
      typeof(SaveModsValidatorShowModsIncompatibilityDialogPatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
    containerDefinition.Bind<ModListViewLocInitializer>().AsSingleton();
    containerDefinition.Bind<ModsIncompatibilityDialog>().AsTransient();
  }
}

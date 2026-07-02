// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.CommonUIPatches;

[Context("MainMenu")]
// ReSharper disable once UnusedType.Global
sealed class ConfiguratorForMainMenu : IConfigurator {
  static readonly string PatchId = typeof(ConfiguratorForMainMenu).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(LoadGameBoxPatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
    containerDefinition.Bind<GameSaveVersionLabelUpdater>().AsSingleton();
  }
}

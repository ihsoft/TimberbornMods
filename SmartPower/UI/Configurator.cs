// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.SmartPower.UI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;
  static readonly Type[] Patches = [
      typeof(ConsumerFragmentServicePatch),
      typeof(GeneratorFragmentServicePatch),
      typeof(NetworkFragmentServicePatch)
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(PatchId, Patches);
  }
}

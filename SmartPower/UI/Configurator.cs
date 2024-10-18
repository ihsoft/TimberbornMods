// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.SmartPower.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.SmartPower.UI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    var patches = new List<Type> { typeof(ConsumerFragmentServicePatch) };
    if (Features.NetworkShowBatteryStats) {
      patches.Add(typeof(NetworkFragmentServicePatch));
    }
    HarmonyPatcher.PatchRepeated(PatchId, patches.ToArray());
  }
}

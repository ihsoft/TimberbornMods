// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.Utils;

namespace IgorZ.TimberCommons.CommonQoLPatches {

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).FullName;

  public void Configure(IContainerDefinition containerDefinition) {
    var patches = new List<Type>();
    if (Features.NoContaminationUnderground) {
      patches.Add(typeof(ContaminationApplierTryApplyContaminationPatch));
      ContaminationApplierTryApplyContaminationPatch.Initialize();
    }
    if (patches.Count == 0) {
      return;
    }
    HarmonyPatcher.PatchRepeated(PatchId, patches.ToArray());
  }
}

}

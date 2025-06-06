﻿// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.MechanicalSystem;
using Timberborn.PowerGenerating;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace IgorZ.SmartPower.PowerGenerators;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  static readonly PrefabPatcher.RequiredComponentsDep GoodConsumingGeneratorDep =
      new(typeof(MechanicalBuildingSpec), typeof(GoodPoweredGeneratorSpec));
  static readonly PrefabPatcher.RequiredComponentsDep WalkerPoweredGeneratorDep =
      new(typeof(MechanicalBuildingSpec), typeof(WalkerPoweredGeneratorSpec));
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.PatchRepeated(PatchId, typeof(GoodPoweredGeneratorPatch));
    CustomizableInstantiator.AddPatcher(PatchId + "-instantiator", PatchMethod);
  }

  static void PatchMethod(GameObject prefab) {
    PrefabPatcher.AddComponent<SmartGoodConsumingGenerator>(prefab, GoodConsumingGeneratorDep.Check);
    PrefabPatcher.AddComponent<SmartWalkerPoweredGenerator>(prefab, WalkerPoweredGeneratorDep.Check);
  }
}

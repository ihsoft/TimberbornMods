// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;

namespace IgorZ.XRay.Patches;

[Context("Game")]
[Context("MapEditor")]
sealed class Configurator : IConfigurator {
  static readonly string PatchId = typeof(Configurator).AssemblyQualifiedName;
  static readonly Type[] Patches = [
      typeof(TileComponentsPatch),
      typeof(TerrainMeshManagerPatch),
      typeof(NaturalResourceModelPatch),
      typeof(NaturalResourcesModelTogglerPatch),
      typeof(SelectableObjectRaycasterPatch),
      typeof(BlockObjectPreviewPickerPatch),
  ];

  public void Configure(IContainerDefinition containerDefinition) {
    HarmonyPatcher.ApplyPatch(PatchId, Patches);
  }
}

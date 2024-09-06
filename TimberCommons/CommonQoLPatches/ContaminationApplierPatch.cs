// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using HarmonyLib;
using TimberApi.DependencyContainerSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.Common;
using Timberborn.Navigation;
using Timberborn.TerrainSystem;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

namespace IgorZ.TimberCommons.CommonQoLPatches {

[HarmonyPatch]
static class ContaminationApplierPatch {
  static ITerrainService _terrainService;

  static MethodBase TargetMethod() {
    return AccessTools.DeclaredMethod(
        "Timberborn.BeaverContaminationSystem.ContaminationApplier:TryApplyContamination");
  }

  static bool Prefix(bool __runOriginal, BaseComponent __instance) {
    if (!__runOriginal) {
      return false;  // The other patches must follow the same style to properly support the skip logic!
    }

    _terrainService ??= DependencyContainer.GetInstance<ITerrainService>();
    var coords = NavigationCoordinateSystem.WorldToGridInt(__instance.TransformFast.position);
    var terrainHeight = _terrainService.CellHeight(coords.XY());
    if (coords.z < terrainHeight) {
      return false;  // Below the surface.
    }
    return true;
  }

  public static void Initialize() {
    _terrainService = null;
  }
}

}

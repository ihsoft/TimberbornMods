// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using TimberApi.DependencyContainerSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BeaverContaminationSystem;
using Timberborn.Common;
using Timberborn.Navigation;
using Timberborn.TerrainSystem;

// ReSharper disable InconsistentNaming
namespace IgorZ.TimberCommons.CommonQoLPatches {

[HarmonyPatch(typeof(ContaminationApplier), nameof(ContaminationApplier.TryApplyContamination))]
static class ContaminationApplierTryApplyContaminationPatch {
  static ITerrainService _terrainService;

  // ReSharper disable once UnusedMember.Local
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

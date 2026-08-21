// Timberborn Mod: Dual District Storage
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using HarmonyLib;
using Timberborn.BlueprintSystem;
using Timberborn.BuildingAvailability;

namespace IgorZ.DualDistrictStorage;

[HarmonyPatch(typeof(BuildingAvailabilityValidator), nameof(BuildingAvailabilityValidator.IsAvailableForPlacement))]
static class BuildingAvailabilityValidatorPatch {
  static void Postfix(ComponentSpec spec, ref bool __result) {
    if (!spec.HasSpec<AsymmetricDualDistrictStoragePartSpec>()) {
      return;
    }

    // Global building consumers, notably build-every-structure achievements, use availability before assuming that a
    // BuildingSpec has a PlaceableBlockObjectSpec. Physical halves are never independently available to the player.
    __result = false;
  }
}

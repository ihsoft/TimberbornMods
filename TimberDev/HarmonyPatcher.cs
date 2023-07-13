// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using UnityDev.Utils.LogUtilsLite;

namespace TimberDev.Utils {

public static class HarmonyPatcher {
  /// <summary>Applies Harmony patches.</summary>
  /// <remarks>This method ensures that no Harmony patches are applied twice.</remarks>
  /// <param name="patchId">Unique patch ID. Duplicated patches for the same ID will be silently ignored.</param>
  /// <param name="patchTypes">Static types that define Harmony patches.</param>
  /// <exception cref="InvalidOperationException">if a patch with <paramref name="patchId"/> was already applied.</exception>
  public static void PatchWithNoDuplicates(string patchId, params Type[] patchTypes) {
    if (Harmony.HasAnyPatches(patchId)) {
      throw new InvalidOperationException("Patch already applied: " + patchId);
    }
    Patch(patchId, patchTypes);
  }

  /// <summary>Applies Harmony patches.</summary>
  /// <remarks>
  /// This method can be called multiple time for the same patch ID. The duplicates will just be ignored.
  /// </remarks>
  /// <param name="patchId">Unique patch ID. Duplicated patches for the same ID will be silently ignored.</param>
  /// <param name="patchTypes">Static types that define Harmony patches.</param>
  public static void PatchRepeated(string patchId, params Type[] patchTypes) {
    if (!Harmony.HasAnyPatches(patchId)) {
      Patch(patchId, patchTypes);
    }
  }

  static void Patch(string patchId, Type[] patchTypes) {
    var harmony = new Harmony(patchId);
    foreach (var type in patchTypes) {
      harmony.PatchAll(type);
      DebugEx.Fine("Harmony patch applied: {0}", type);
    }
  }
}

}

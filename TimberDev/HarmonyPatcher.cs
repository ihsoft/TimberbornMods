// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using UnityDev.Utils.LogUtilsLite;

namespace TimberDev.Utils {

public static class HarmonyPatcher {
  public static void PatchWithNoDuplicates(string patchId, params Type[] patchTypes) {
    if (Harmony.HasAnyPatches(patchId)) {
      DebugEx.Warning("Skip duplicated patches: {0}", patchId);
      return;
    }
    var harmony = new Harmony(patchId);
    foreach (var type in patchTypes) {
      harmony.PatchAll(type);
      DebugEx.Fine("Harmony patch applied: {0}", type);
    }
  }
}

}

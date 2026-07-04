// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Utils;
using Timberborn.SingletonSystem;

namespace IgorZ.SmartHaulers.Patches;

sealed class SmartHaulersPatcher : ILoadableSingleton {
  const string PatchId = "IgorZ.SmartHaulers";

  public void Load() {
    HarmonyPatcher.ApplyPatch(
        PatchId,
        typeof(CriticalInventoryNeedActionPatch),
        typeof(CriticalInventoryNeedReroutePatch));
  }
}

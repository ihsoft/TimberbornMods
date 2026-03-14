using System.Collections.Generic;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.Common;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local

namespace TestParser.Stubs.Patches;

[HarmonyPatch(typeof(BaseComponent))]
static class BaseComponentPatch {
  static readonly List<object> TestComponents = [new Foobar()];

  [HarmonyPrefix]
  [HarmonyPatch(nameof(BaseComponent.AllComponents), MethodType.Getter)]
  static bool ReturnConstantList(out ReadOnlyList<object> __result) {
    __result = new ReadOnlyList<object>(TestComponents);
    return false;
  }

  [HarmonyPrefix]
  [HarmonyPatch("op_Implicit")]
  static bool SkipGameObjectCheck(BaseComponent baseComponent, out bool __result) {
    __result = baseComponent != null;
    return false;
  }
}

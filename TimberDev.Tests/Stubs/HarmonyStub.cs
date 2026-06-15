using System;

namespace HarmonyLib;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HarmonyPatch : Attribute {
  public HarmonyPatch(Type type, string methodName) {
  }
}

using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.BlueprintSystem;

namespace TimberDev.Tests;

static class CustomizableInstantiatorTests {
  public static void AddPatcherReplacesById() {
    Reset();
    var firstCalls = 0;
    var secondCalls = 0;
    var blueprint = new Blueprint();
    var components = new List<object>();

    CustomizableInstantiator.AddPatcher("patch", (b, result) => firstCalls++);
    CustomizableInstantiator.AddPatcher("patch", (b, result) => {
      Assert.Equal(blueprint, b);
      Assert.Equal(components, result);
      secondCalls++;
    });

    InvokePostfix(blueprint, components);

    Assert.Equal(1, HarmonyPatcherUnpatcher.Patches.Count);
    Assert.Equal(0, firstCalls);
    Assert.Equal(1, secondCalls);
  }

  static void InvokePostfix(Blueprint blueprint, List<object> components) {
    var nestedType = typeof(CustomizableInstantiator).GetNestedType(
        "BaseInstantiatorPatch",
        BindingFlags.NonPublic);
    var postfix = nestedType.GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
    postfix.Invoke(null, [blueprint, components]);
  }

  static void Reset() {
    new HarmonyPatcherUnpatcher().Unload();
    Harmony.Reset();

    var nestedType = typeof(CustomizableInstantiator).GetNestedType(
        "BaseInstantiatorPatch",
        BindingFlags.NonPublic);
    var patchers = (Dictionary<string, System.Action<Blueprint, List<object>>>)nestedType
        .GetField("Patchers", BindingFlags.Static | BindingFlags.Public)
        .GetValue(null);
    patchers.Clear();
  }
}

using System;
using System.Linq;
using System.Reflection;
using Bindito.Core;
using HarmonyLib;
using IgorZ.TimberDev.Utils;

namespace TimberDev.Tests;

static class HarmonyPatcherTests {
  public static void ApplyPatchRegistersAndRejectsDuplicateId() {
    ResetHarmonyPatcher();

    HarmonyPatcher.ApplyPatch("test.patch", typeof(TestPatchA), typeof(TestPatchB));

    Assert.True(Harmony.HasAnyPatches("test.patch"));
    Assert.Equal(1, HarmonyPatcherUnpatcher.Patches.Count);
    Assert.Equal("test.patch", HarmonyPatcherUnpatcher.Patches[0].patchId);
    Assert.Equal(2, HarmonyPatcherUnpatcher.Patches[0].processors.Count);
    Assert.True(HarmonyPatcherUnpatcher.Patches[0].processors.All(processor => processor.IsPatched));
    Assert.Throws<InvalidOperationException>(() => HarmonyPatcher.ApplyPatch("test.patch", typeof(TestPatchA)));
  }

  public static void UnpatcherRemovesPatchesInReverseOrder() {
    ResetHarmonyPatcher();
    HarmonyPatcher.ApplyPatch("first.patch", typeof(TestPatchA));
    HarmonyPatcher.ApplyPatch("second.patch", typeof(TestPatchB));

    new HarmonyPatcherUnpatcher().Unload();

    Assert.False(Harmony.HasAnyPatches("first.patch"));
    Assert.False(Harmony.HasAnyPatches("second.patch"));
    Assert.Equal(0, HarmonyPatcherUnpatcher.Patches.Count);
  }

  public static void ConfiguratorRegistersUnpatcherAsSingleton() {
    var configurator = new HarmonyPatcherConfigurator();
    var containerDefinition = new TestContainerDefinition();

    configurator.Configure(containerDefinition);

    Assert.Equal(1, containerDefinition.Bindings.Count);
    Assert.Equal(typeof(HarmonyPatcherUnpatcher), containerDefinition.Bindings[0].Type);
    Assert.True(containerDefinition.Bindings[0].Singleton);
  }

  public static void ConfiguratorDeclaresAllContexts() {
    var contexts = typeof(HarmonyPatcherConfigurator)
        .GetCustomAttributes<Context>()
        .Select(attribute => attribute.Name)
        .OrderBy(name => name)
        .ToArray();

    Assert.Equal("Bootstrapper", contexts[0]);
    Assert.Equal("Game", contexts[1]);
    Assert.Equal("MainMenu", contexts[2]);
    Assert.Equal("MapEditor", contexts[3]);
    Assert.Equal(4, contexts.Length);
  }

  static void ResetHarmonyPatcher() {
    new HarmonyPatcherUnpatcher().Unload();
    Harmony.Reset();
  }

  sealed class TestPatchA {
  }

  sealed class TestPatchB {
  }
}

// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using HarmonyLib;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.Utils;

/// <summary>Helper class to safely apply harmony patches.</summary>
static class HarmonyPatcher {
  /// <summary>Applies Harmony patches.</summary>
  /// <remarks>
  /// This method ensures that no Harmony patches are applied twice. The patch will only live during the game session.
  /// It will be undone when game reloads or exists to main menu.
  /// </remarks>
  /// <param name="patchId">Unique patch ID. Duplicated patches for the same ID will trigger an error.</param>
  /// <param name="patchTypes">Static types that define Harmony patches.</param>
  /// <exception cref="InvalidOperationException">if a patch with <paramref name="patchId"/> was already applied.</exception>
  public static void ApplyPatch(string patchId, params Type[] patchTypes) {
    if (Harmony.HasAnyPatches(patchId)) {
      throw new InvalidOperationException("Patch already applied: " + patchId);
    }
    Patch(patchId, patchTypes);
  }

  static void Patch(string patchId, Type[] patchTypes) {
    var harmony = new Harmony(patchId);
    var processors = new List<PatchClassProcessor>();
    foreach (var type in patchTypes) {
      var processor = harmony.CreateClassProcessor(type);
      processors.Add(processor);
      processor.Patch();
    }
    HarmonyPatcherUnpatcher.Patches.Add((patchId, processors));
    DebugEx.Info("Harmony patch applied: {0}", patchId);
  }
}

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class HarmonyPatcherConfigurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<HarmonyPatcherUnpatcher>().AsSingleton();
  }
}

sealed class HarmonyPatcherUnpatcher : IUnloadableSingleton {
  internal static readonly List<(string patchId, List<PatchClassProcessor> processors)> Patches = [];

  public void Unload() {
    for (var i = Patches.Count - 1; i >= 0; i--) {
      var patch = Patches[i];
      DebugEx.Info("Removing Harmony patch: {0}", patch.patchId);
      for (var j = patch.processors.Count - 1; j >= 0; j--) {
        patch.processors[j].Unpatch();
      }
    }
    Patches.Clear();
  }
}

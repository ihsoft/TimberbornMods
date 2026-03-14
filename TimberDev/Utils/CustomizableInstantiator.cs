// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using HarmonyLib;
using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;

// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.Utils;

/// <summary>Class that allows customizing components on prefabs.</summary>
/// <remarks>
/// For every prefab used in the game, a custom code will be called before creating any instances. It allows
/// customizing the prefab. For example, a new component can be added, an existing component replaced, or the prefab
/// components can be configured differently from their normal asset state.
/// </remarks>
static class CustomizableInstantiator {
  static readonly string HarmonyPatchId = typeof(CustomizableInstantiator).AssemblyQualifiedName;

  /// <summary>Adds a patcher method. Must be called from the <c>InGame</c> configurator.</summary>
  /// <remarks>
  /// The patcher method will be called exactly once for every unique prefab. The prefab patchers are re-applied on
  /// every game load.
  /// </remarks>
  /// <param name="patchId">
  /// Unique id of the patch. Adding multiple patchers with the same ID will result in overwriting the old patch.
  /// </param>
  /// <param name="patchFn">The method to call on the prefab being patched.</param>
  public static void AddPatcher(string patchId, Action<Blueprint, List<object>> patchFn) {
    if (!Harmony.HasAnyPatches(HarmonyPatchId)) {
      HarmonyPatcher.ApplyPatch(HarmonyPatchId, typeof(BaseInstantiatorPatch));
    }
    BaseInstantiatorPatch.Patchers[patchId] = patchFn;
  }

  [HarmonyPatch(typeof(BaseInstantiator), nameof(BaseInstantiator.InstantiateComponents))]
  static class BaseInstantiatorPatch {
    public static readonly Dictionary<string, Action<Blueprint, List<object>>> Patchers = new();

    static void Postfix(Blueprint blueprint, List<object> __result) {
      foreach (var patcher in Patchers.Values) {
        patcher(blueprint, __result);
      }
    }
  }
}
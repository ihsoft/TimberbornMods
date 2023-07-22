// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Unity;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.CustomInstantiator {

/// <summary>Class that allows customizing components on prefabs.</summary>
/// <remarks>
/// For every prefab that is used in the game a custom code will be called before creating any instances. It allows
/// customizing the prefab. E.g. a new component can be added, an existing component replaced, or the prefab components
/// can be configured differently from their normal asset state.
/// </remarks>
/// <seealso cref="PrefabPatcher"/>
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
  public static void AddPatcher(string patchId, Action<GameObject> patchFn) {
    HarmonyPatcher.PatchRepeated(HarmonyPatchId, typeof(PrefabInstantiatePatch));
    PrefabInstantiatePatch.Patchers[patchId] = patchFn;
    PrefabInstantiatePatch.PatchedCache.Clear();
  }

  [HarmonyPatch(typeof(Instantiator), nameof(Instantiator.InstantiateInactive))]
  static class PrefabInstantiatePatch {
    public static readonly HashSet<string> PatchedCache = new();
    public static readonly Dictionary<string, Action<GameObject>> Patchers = new();

    static void Prefix(GameObject prefab) {
      if (!PatchedCache.Add(prefab.name)) {
        return;
      }
      foreach (var patcher in Patchers.Values) {
        patcher(prefab);
      }
    }
  }
}

}

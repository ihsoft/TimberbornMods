// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Bindito.Core;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService {

/// <summary>Class that allows setting overrides to the soil moisture levels.</summary>
/// <remarks>
/// This code uses HarmonyX patches to access internal game's logic. Significant changes to it may break the mod.
/// </remarks>
public class DirectSoilMoistureSystemAccessor : IPostLoadableSingleton, ITickableSingleton {
  #region API
  /// <summary>Creates moisture level overrides for a set of tiles.</summary>
  /// <remarks>
  /// The same tiles can be listed in multiple overrides. In this case the maximum level will be used.
  /// </remarks>
  /// <param name="tiles">The tiles to apply the override to.</param>
  /// <param name="moisture">The moister level.</param>
  /// <returns>Unique ID of the created override. Use it to delete the overrides.</returns>
  /// <seealso cref="RemoveMoistureOverride"/>
  public int AddMoistureOverride(IEnumerable<Vector2Int> tiles, float moisture) {
    var index = _nextOverrideId++;
    _moistureOverrides.Add(
        index,
        tiles.Distinct().Select(c => _mapIndexService.CoordinatesToIndex(c)).ToDictionary(k => k, _ => moisture));
    _needCacheUpdate = true;
    return index;
  }

  /// <summary>Removes the override.</summary>
  /// <param name="overrideId">The ID of the override to remove.</param>
  /// <seealso cref="AddMoistureOverride"/>
  public void RemoveMoistureOverride(int overrideId) {
    _needCacheUpdate = _moistureOverrides.Remove(overrideId);
  }
  #endregion

  #region IPostLoadableSingleton implementation
  /// <summary>Sets up the moisture override logic.</summary>
  public void PostLoad() {
    DebugEx.Fine("Initializing direct access to SoilMoistureSystem...");
    HarmonyPatcher.PatchRepeated(GetType().AssemblyQualifiedName, typeof(SoilMoistureSimulatorPatch));
    SoilMoistureSimulatorPatch.MoistureOverrides = null;
  }
  #endregion

  #region ITickableSingleton implementation
  /// <summary>Updates the moisture cache and creates a thread safe copy.</summary>
  public void Tick() {
    if (_needCacheUpdate) {
      _needCacheUpdate = false;
      var overridesCache = new Dictionary<int, float>();
      foreach (var value in _moistureOverrides.Values.SelectMany(item => item)) {
        if (overridesCache.TryGetValue(value.Key, out var existingValue)) {
          overridesCache[value.Key] = Mathf.Max(value.Value, existingValue);
        } else {
          overridesCache.Add(value.Key, value.Value);
        }
      }
      DebugEx.Fine("Updating the list of moisture overrides: old={0}, new={1}",
                   SoilMoistureSimulatorPatch.MoistureOverrides.Count, overridesCache.Count);
      SoilMoistureSimulatorPatch.MoistureOverrides = overridesCache;
    }
  }
  #endregion

  #region Implementation
  readonly Dictionary<int, Dictionary<int, float>> _moistureOverrides = new();
  int _nextOverrideId = 1;
  bool _needCacheUpdate;
  MapIndexService _mapIndexService;

  /// <summary>Injects run-time dependencies.</summary>
  [Inject]
  public void InjectDependencies(MapIndexService mapIndexService) {
    _mapIndexService = mapIndexService;
  }
  #endregion

  #region Harmony patch
  [HarmonyPatch]
  [SuppressMessage("ReSharper", "UnusedMember.Local")]
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static class SoilMoistureSimulatorPatch {
    const string SoilMoistureSimulatorClassName = "Timberborn.SoilMoistureSystem.SoilMoistureSimulator";
    const string MethodName = "GetUpdatedMoisture";

    public static Dictionary<int, float> MoistureOverrides;
    
    static MethodBase TargetMethod() {
      var type = AccessTools.TypeByName(SoilMoistureSimulatorClassName);
      var methodBase = AccessTools.FirstMethod(type, method => method.Name == MethodName);
      return methodBase;
    }

    static void Postfix(int index, ref bool __runOriginal, ref float __result) {
      if (!__runOriginal) {
        return;  // The other patches must follow the same style to properly support the skip logic!
      }
      if (MoistureOverrides != null && MoistureOverrides.TryGetValue(index, out var newLevel)) {
        __result = __result < newLevel ? newLevel : __result;
        __runOriginal = false;
      }
    }
  }
  #endregion
}

}

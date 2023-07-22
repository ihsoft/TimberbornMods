// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using TimberApi.DependencyContainerSystem;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.WaterSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService {

/// <summary>
/// Class that allows accessing to the internal water system logic. Be careful using it! The internal logic runs in
/// threads, so it's not safe to access anything anytime.
/// </summary>
/// <remarks>
/// This code interacts with the game's water system via reflections to the internal classes and properties. If the
/// relevant accessors cannot be obtained, then <c>DirectWaterServiceAccessor</c> goes into invalid state. Clients
/// must check for <see cref="IsValid"/> before trying to use direct access. It's a good idea to have backup code in the
/// client for this case.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class DirectWaterServiceAccessor : IPostLoadableSingleton, ILateUpdatableSingleton {
  object _waterMapObj;
  PropertyInfo _flowsPropertyFn;
  PropertyInfo _depthsPropertyFn;

  class WaterConsumer {
    public float WaterFlow;
    public float WaterTaken;
    public float WaterShortage;

    public WaterConsumer Copy() {
      // ReSharper disable once UseObjectOrCollectionInitializer
      var copy = new WaterConsumer();
      copy.WaterFlow = WaterFlow;
      copy.WaterShortage = WaterShortage;
      copy.WaterTaken = WaterTaken;
      return copy;
    }
  } 
  readonly Dictionary<int, WaterConsumer> _waterConsumers = new();
  Dictionary<int, WaterConsumer> _threadSafeWaterConsumers = new();

  #region API
  /// <summary>Shortcut to the map index service to get the tile indexes.</summary>
  public MapIndexService MapIndexService { get; private set; }

  /// <summary>Water depths indexed by the tile index.</summary>
  /// <remarks>
  /// <p>
  /// This array specifies the amount of water above the solid ground or obstacle. The values are not the absolute water
  /// heights.
  /// </p>
  /// <p>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </p>
  /// </remarks>
  public float[] WaterDepths { get; private set; }

  /// <summary>Water flows indexed by the tile index.</summary>
  /// <remarks>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </remarks>
  public WaterFlow[] WaterFlows { get; private set; }

  /// <summary>Indicates if the direct water system access can be used.</summary>
  public bool IsValid { get; private set; }

  /// <summary>Sets up a water consumed at the tile.</summary>
  /// <remarks>
  /// This method can be called from the main thread as frequent as needed, but the actual simulation logic will be
  /// updated on the next tick.
  /// </remarks>
  /// <param name="tileIndex">Index of the tile (see
  /// <see cref="Timberborn.MapIndexSystem.MapIndexService.CoordinatesToIndex"/>). It can be a new tile or an existing
  /// consumer.
  /// </param>
  /// <param name="waterDemandPerSecond">
  /// Desired water consumption per second. A less amount of water can be actually consumed if there is not enough
  /// supply.
  /// </param>
  /// <seealso cref="FlushWaterStats"/>
  public void SetWaterConsumer(int tileIndex, float waterDemandPerSecond) {
    if (!_waterConsumers.TryGetValue(tileIndex, out var consumer)) {
      _waterConsumers.Add(tileIndex, new WaterConsumer { WaterFlow = waterDemandPerSecond });
    } else {
      consumer.WaterFlow = waterDemandPerSecond;
    }
  }

  /// <summary>Removes water consumer for the tile.</summary>
  /// <remarks>
  /// This method can be called from the main thread as frequent as needed, but the actual simulation logic will be
  /// updated on the next tick.
  /// </remarks>
  /// <param name="tileIndex">Index of the tile (see
  /// <see cref="Timberborn.MapIndexSystem.MapIndexService.CoordinatesToIndex"/>). It's not required to specify an
  /// existing water consumer.
  /// </param>
  public void DeleteWaterConsumer(int tileIndex) {
    _waterConsumers.Remove(tileIndex);
  }

  /// <summary>Returns accumulated water consumptions stat and resets the counters for the tile.</summary>
  /// <param name="tileIndex">Index of the tile (see
  /// <see cref="Timberborn.MapIndexSystem.MapIndexService.CoordinatesToIndex"/>). The tile must be a water consumer.
  /// </param>
  /// <param name="takenWater">The amount of water that was actually taken from the scene.</param>
  /// <param name="waterShortage">
  /// The amount of water that was requested, but not taken from the scene due to there was not enough supply.
  /// </param>
  /// <exception cref="InvalidOperationException">if not water consumer at the tile.</exception>
  /// <seealso cref="SetWaterConsumer"/>
  public void FlushWaterStats(int tileIndex, out float takenWater, out float waterShortage) {
    if (!_waterConsumers.TryGetValue(tileIndex, out var consumer)) {
      throw new InvalidOperationException($"No water consumer at tile index {tileIndex}");
    }
    takenWater = consumer.WaterTaken;
    waterShortage = consumer.WaterShortage;
    consumer.WaterTaken = consumer.WaterShortage = 0;
  }
  #endregion

  #region IPostLoadableSingleton implementation
  /// <summary>Gets accessors to the water system internal classes and properties.</summary>
  /// <remarks>It's expected to ev called after all the game state is loaded and ready.</remarks>
  public void PostLoad() {
    DebugEx.Fine("Initializing direct access to WaterSystem...");

    var waterServiceAssembly = typeof(IWaterService).Assembly;
    var waterMapType = waterServiceAssembly.GetType("Timberborn.WaterSystem.WaterMap");
    if (waterMapType == null) {
      DebugEx.Warning("Cannot get WaterMap type. DirectWaterSystem is inactive.");
      return;
    }
    _flowsPropertyFn = waterMapType.GetProperty("Outflows");
    _depthsPropertyFn = waterMapType.GetProperty("WaterDepths");
    if (_flowsPropertyFn == null || _depthsPropertyFn == null) {
      DebugEx.Warning("Cannot get access to WaterMap type. DirectWaterSystem is inactive.");
      return;
    }
    _waterMapObj = DependencyContainer.GetInstance(waterMapType);
    if (_waterMapObj == null) {
      DebugEx.Warning("Cannot obtain WaterMap instance. DirectWaterSystem is inactive.");
      return;
    }
    WaterDepths = _depthsPropertyFn.GetValue(_waterMapObj) as float[];
    WaterFlows = _flowsPropertyFn.GetValue(_waterMapObj) as WaterFlow[];
    if (WaterDepths == null || WaterFlows == null) { // This is unexpected!
      throw new InvalidOperationException("Cannot get data from WaterMap");
    }
    MapIndexService = DependencyContainer.GetInstance<MapIndexService>();
    if (MapIndexService == null) { // This is unexpected!
      throw new InvalidOperationException("Cannot get MapIndexService instance");
    }

    HarmonyPatcher.PatchRepeated(GetType().AssemblyQualifiedName, typeof(WaterSimulatorWaterDepthsPatch));
    WaterSimulatorWaterDepthsPatch.DirectWaterServiceAccessor = this;
    IsValid = true;
  }
  #endregion

  #region ILateUpdatableSingleton implementation
  /// <summary>Updates stats in the water consumers and creates a thread safe copy.</summary>
  public void LateUpdateSingleton() {
    var newConsumers = new Dictionary<int, WaterConsumer>();
    foreach (var item in _waterConsumers) {
      var itemValue = item.Value;
      if (_threadSafeWaterConsumers.TryGetValue(item.Key, out var existing)) {
        itemValue.WaterTaken += existing.WaterTaken;
        itemValue.WaterShortage += existing.WaterShortage;
      }
      newConsumers.Add(item.Key, new WaterConsumer { WaterFlow = itemValue.WaterFlow });
    }
    _threadSafeWaterConsumers = newConsumers;
  }
  #endregion

  #region Implementation
  /// <summary>
  /// Processes the water consumption. Must only be called from the thread that is processing the water height updates. 
  /// </summary>
  /// <param name="deltaTime">Simulation step delta.</param>
  void UpdateDepthsCallback(float deltaTime) {
    foreach (var item in _threadSafeWaterConsumers) {
      var inputIndex = item.Key;
      var consumer = item.Value;
      var depth = WaterDepths[inputIndex];
      var needAmount = consumer.WaterFlow * deltaTime;
      var canTakeAmount = Mathf.Min(needAmount, depth);
      if (canTakeAmount < needAmount) {
        consumer.WaterShortage += needAmount - canTakeAmount;
      }
      WaterDepths[inputIndex] -= canTakeAmount;
      consumer.WaterTaken += canTakeAmount;
    }
  }
  #endregion

  #region WaterSimulator Harmony patch
  [HarmonyPatch]
  [SuppressMessage("ReSharper", "UnusedMember.Local")]
  [SuppressMessage("ReSharper", "InconsistentNaming")]
  static class WaterSimulatorWaterDepthsPatch {
    const string NetworkFragmentServiceClassName = "Timberborn.WaterSystem.WaterSimulator";
    const string MethodName = "UpdateWaterDepths";

    public static DirectWaterServiceAccessor DirectWaterServiceAccessor;

    static MethodBase TargetMethod() {
      var type = AccessTools.TypeByName(NetworkFragmentServiceClassName);
      return AccessTools.FirstMethod(type, method => method.Name == MethodName);
    }

    static void Postfix(float ____deltaTime) {
      DirectWaterServiceAccessor?.UpdateDepthsCallback(____deltaTime);
    }
  }
  #endregion
}

}

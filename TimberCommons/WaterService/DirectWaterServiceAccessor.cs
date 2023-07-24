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
  /// <summary>Water mover definition.</summary>
  /// <remarks>
  /// The mover takes water from the inout and drops it at the output. Various settings allow adjusting the exact
  /// behavior of the mover.
  /// </remarks>
  public class WaterMover {
    internal WaterMover ThreadSafeWaterMover;
    internal bool LogExtraStats;

    /// <summary>Index of the tile to get water from.</summary>
    public int InputTileIndex => _inputTileIndex;
    internal int _inputTileIndex;

    /// <summary>Index of the tile to drop the water to.</summary>
    public int OutputTileIndex => _outputTileIndex;
    internal int _outputTileIndex;

    /// <summary>
    /// In <see cref="FreeFlow"/> mode this is the maximum allowed flow. Otherwise, it's a desirable flow that the mover
    /// will try to achieve, given there is enough water supply at the input.
    /// </summary>
    public float WaterFlow;

    /// <summary>Accumulated amount of moved water. Reset it to 0 to measure flow between the ticks.</summary>
    public float WaterMoved;

    /// <summary>Tells the logic to check if the water level at output is not above the input tile level.</summary>
    public bool FreeFlow;

    /// <summary>
    /// The maximum absolute water height to keep at the outtake. No water will be moved if the level is already high.
    /// </summary>
    /// <remarks>Set it to a negative value to indicate that this chek is not needed.</remarks>
    /// <seealso cref="DirectWaterServiceAccessor.SurfaceHeights"/>
    public float MaxHeightAtOutput = -1;

    /// <summary>
    /// The minimum absolute water height to keep at the outtake. No water will be moved if the level is already high.
    /// </summary>
    /// <remarks>Set it to a negative value to indicate that this chek is not needed.</remarks>
    /// <seealso cref="DirectWaterServiceAccessor.SurfaceHeights"/>
    public float MinHeightAtInput = -1;

    public WaterMover(int inputTileIndex, int outputTileIndex) {
      _inputTileIndex = inputTileIndex;
      _outputTileIndex = outputTileIndex;
    }

    internal WaterMover CopyDefinition() {
      return new WaterMover(_inputTileIndex, _outputTileIndex) {
          WaterFlow = WaterFlow,
          FreeFlow = FreeFlow,
          MaxHeightAtOutput = MaxHeightAtOutput,
          MinHeightAtInput = MinHeightAtInput,
          LogExtraStats = LogExtraStats,
      };
    }

    internal void UpdateSettingsFrom(WaterMover source) {
      WaterFlow = source.WaterFlow;
      FreeFlow = source.FreeFlow;
      MaxHeightAtOutput = source.MaxHeightAtOutput;
      MinHeightAtInput = source.MinHeightAtInput;
      LogExtraStats = source.LogExtraStats;
    }
  } 
  readonly List<WaterMover> _waterMovers = new();
  List<WaterMover> _threadSafeWaterMovers;

  #region API
  /// <summary>Shortcut to the map index service.</summary>
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
  /// <seealso cref="CoordinatesToIndex"/>
  public float[] WaterDepths => _waterDepths;
  float[] _waterDepths;

  /// <summary>Water flows indexed by the tile index.</summary>
  /// <remarks>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </remarks>
  /// <seealso cref="CoordinatesToIndex"/>
  public WaterFlow[] WaterFlows => _waterFlows;
  WaterFlow[] _waterFlows;

  /// <summary>Water height bases indexed by the tile index.</summary>
  /// <remarks>
  /// <p>This height accounts both the terrain and the water obstacle blocks.</p>
  /// <p>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </p>
  /// </remarks>
  /// <seealso cref="CoordinatesToIndex"/>
  public int[] SurfaceHeights => _surfaceHeights;
  int[] _surfaceHeights;

  /// <summary>Indicates if the direct water system access can be used.</summary>
  public bool IsValid { get; private set; }

  /// <summary>Shortcut method to covert coordinates to tile index.</summary>
  public int CoordinatesToIndex(Vector2Int coordinates) {
    return MapIndexService.CoordinatesToIndex(coordinates);
  }

  /// <summary>Ads a new water mover.</summary>
  /// <remarks>
  /// This method can be called from the main thread as frequent as needed, but the actual simulation logic will be
  /// updated on the next tick.
  /// </remarks>
  /// <param name="waterMover">Mover definition. All required fields musty be properly filled.</param>
  public void AddWaterMover(WaterMover waterMover) {
    _waterMovers.Add(waterMover);
  }

  /// <summary>Removes the specified water mover.</summary>
  /// <remarks>
  /// This method can be called from the main thread as frequent as needed, but the actual simulation logic will be
  /// updated on the next tick.
  /// </remarks>
  public void DeleteWaterMover(WaterMover waterMover) {
    _waterMovers.Remove(waterMover);
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
    var flowsPropertyFn = waterMapType.GetProperty("Outflows");
    var depthsPropertyFn = waterMapType.GetProperty("WaterDepths");
    if (flowsPropertyFn == null || depthsPropertyFn == null) {
      DebugEx.Warning("Cannot get access to WaterMap type. DirectWaterSystem is inactive.");
      return;
    }
    var waterMapObj = DependencyContainer.GetInstance(waterMapType);
    _waterDepths = depthsPropertyFn.GetValue(waterMapObj) as float[];
    _waterFlows = flowsPropertyFn.GetValue(waterMapObj) as WaterFlow[];
    if (WaterDepths == null || WaterFlows == null) { // This is unexpected!
      throw new InvalidOperationException("Cannot get data from WaterMap");
    }
    MapIndexService = DependencyContainer.GetInstance<MapIndexService>();
    if (MapIndexService == null) { // This is unexpected!
      throw new InvalidOperationException("Cannot get MapIndexService instance");
    }

    var surfaceServiceType = waterServiceAssembly.GetType("Timberborn.WaterSystem.ImpermeableSurfaceService");
    if (surfaceServiceType == null) {
      DebugEx.Warning("Cannot get ImpermeableSurfaceService type. DirectWaterSystem is inactive.");
      return;
    }
    var heightPropertyFn = surfaceServiceType.GetProperty("Heights");
    if (heightPropertyFn == null) {
      DebugEx.Warning("Cannot get access to ImpermeableSurfaceService type. DirectWaterSystem is inactive.");
      return;
    }
    _surfaceHeights = heightPropertyFn.GetValue(DependencyContainer.GetInstance(surfaceServiceType)) as int[];

    HarmonyPatcher.PatchRepeated(GetType().AssemblyQualifiedName, typeof(WaterSimulatorWaterDepthsPatch));
    WaterSimulatorWaterDepthsPatch.DirectWaterServiceAccessor = this;
    IsValid = true;
  }
  #endregion

  #region ILateUpdatableSingleton implementation
  /// <summary>Updates stats in the water consumers and creates a thread safe copy.</summary>
  public void LateUpdateSingleton() {
    var newMovers = new List<WaterMover>();
    foreach (var waterMover in _waterMovers) {
      if (waterMover.InputTileIndex == -1 || waterMover.OutputTileIndex == -1) {
        throw new InvalidOperationException("Water consumers and givers are not supported yet!");
      }
      var threadSafeMover = waterMover.ThreadSafeWaterMover;
      if (threadSafeMover != null) {
        waterMover.WaterMoved += threadSafeMover.WaterMoved;
        threadSafeMover.WaterMoved = 0;
        threadSafeMover.UpdateSettingsFrom(waterMover);
      } else {
        threadSafeMover = waterMover.CopyDefinition();
        waterMover.ThreadSafeWaterMover = threadSafeMover;
      }
      newMovers.Add(threadSafeMover);
    }
    _threadSafeWaterMovers = newMovers;
  }
  #endregion

  #region Implementation
  /// <summary>
  /// Processes the water consumption. Must only be called from the thread that is processing the water height updates. 
  /// </summary>
  /// <param name="deltaTime">Simulation step delta.</param>
  void UpdateDepthsCallback(float deltaTime) {
    for (var i = _threadSafeWaterMovers.Count - 1; i >= 0; i--) {
      var waterMover = _threadSafeWaterMovers[i];
      var inputIndex = waterMover._inputTileIndex;
      var outputIndex = waterMover._outputTileIndex;
      var needAmount = waterMover.WaterFlow * deltaTime;
      var inDepth = _waterDepths[inputIndex];
      var inWaterHeight = _surfaceHeights[inputIndex] + inDepth;
      var outWaterHeight = _surfaceHeights[outputIndex] + _waterDepths[outputIndex];
      var canTakeAmount = Mathf.Min(needAmount, inDepth);
      if (canTakeAmount < float.Epsilon) {
        continue;
      }
      if (waterMover.FreeFlow) {
        var waterDiff = (inWaterHeight - outWaterHeight) / 2;
        if (waterDiff < float.Epsilon) {
          continue;
        }
        if (waterDiff < canTakeAmount) {
          canTakeAmount = waterDiff;
        }
      }
      if (waterMover.MinHeightAtInput > 0) {
        var waterToTarget = inWaterHeight - waterMover.MinHeightAtInput;
        if (waterToTarget < float.Epsilon) {
          continue;
        }
        if (waterToTarget < canTakeAmount) {
          canTakeAmount = waterToTarget;
        }
      }
      if (waterMover.MaxHeightAtOutput > 0) {
        var waterToTarget = waterMover.MaxHeightAtOutput - outWaterHeight;
        if (waterToTarget < float.Epsilon) {
          continue;
        }
        if (waterToTarget < canTakeAmount) {
          canTakeAmount = waterToTarget;
        }
      }
      _waterDepths[inputIndex] -= canTakeAmount;
      _waterDepths[outputIndex] += canTakeAmount;
      waterMover.WaterMoved += canTakeAmount;
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

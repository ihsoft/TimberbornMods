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
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
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
public class DirectWaterServiceAccessor : IPostLoadableSingleton, ITickableSingleton {
  /// <summary>Water mover definition.</summary>
  /// <remarks>
  /// The mover takes water from the inout and drops it at the output. Various settings allow adjusting the exact
  /// behavior of the mover.
  /// </remarks>
  public class WaterMover {
    internal WaterMover ThreadSafeWaterMover;
    internal bool LogExtraStats;

    /// <summary>Index of the tile to get water from.</summary>
    /// <remarks>If set to <c>-1</c>, then only the mover will only be adding water.</remarks>
    /// <seealso cref="DropWaterLimit"/>
    public int InputTileIndex = -1;

    /// <summary>Index of the tile to drop the water to.</summary>
    /// <remarks>If set to <c>-1</c>, then the mover will only be consuming water.</remarks>
    /// <seealso cref="ConsumeWaterLimit"/>
    public int OutputTileIndex = -1;

    /// <summary>
    /// In <see cref="FreeFlow"/> mode this is the maximum allowed flow. Otherwise, it's a desirable flow that the mover
    /// will try to achieve, given there is enough water supply at the input.
    /// </summary>
    /// <remarks>The flow must not be a negative value.</remarks>
    public float WaterFlow;

    /// <summary>Accumulated amount of moved water.</summary>
    /// <seealso cref="DropWaterLimit"/>
    /// <seealso cref="ConsumeWaterLimit"/>
    public float WaterMoved;

    /// <summary>Maximum amount of water to drop at the outtake.</summary>
    /// <remarks>
    /// Water mover stops when <see cref="WaterMoved"/> reaches this value. If set to a negative value, then the limit
    /// is not checked.
    /// </remarks>
    public float DropWaterLimit = -1;

    /// <summary>Maximum amount of water that can be consumed at the intake.</summary>
    /// <remarks>
    /// Water mover stops when <see cref="WaterMoved"/> reaches this value. If set to a negative value, then the limit
    /// is not checked.
    /// </remarks>
    public float ConsumeWaterLimit = -1;

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

    /// <inheritdoc/>
    public override string ToString() {
      return string.Format("[WaterMover#in={0},out={1},flow={2},free={3},inMin={4},outMax={5}]",
          InputTileIndex, OutputTileIndex, WaterFlow, FreeFlow, MinHeightAtInput, MaxHeightAtOutput);
    }

    internal WaterMover CopyDefinition() {
      return new WaterMover {
          InputTileIndex = InputTileIndex,
          OutputTileIndex = OutputTileIndex,
          DropWaterLimit = DropWaterLimit,
          ConsumeWaterLimit = ConsumeWaterLimit,
          WaterFlow = WaterFlow,
          FreeFlow = FreeFlow,
          MaxHeightAtOutput = MaxHeightAtOutput,
          MinHeightAtInput = MinHeightAtInput,
          LogExtraStats = LogExtraStats,
      };
    }

    internal void UpdateSettingsFrom(WaterMover source) {
      InputTileIndex = source.InputTileIndex;
      OutputTileIndex = source.OutputTileIndex;
      WaterFlow = source.WaterFlow;
      FreeFlow = source.FreeFlow;
      MaxHeightAtOutput = source.MaxHeightAtOutput;
      MinHeightAtInput = source.MinHeightAtInput;
      LogExtraStats = source.LogExtraStats;
    }
  } 
  readonly List<WaterMover> _waterMovers = new();
  List<WaterMover> _threadSafeWaterMovers = new();

  #region API
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
  public float[] WaterDepths => _waterDepths;
  float[] _waterDepths;

  /// <summary>Water flows indexed by the tile index.</summary>
  /// <remarks>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </remarks>
  public WaterFlow[] WaterFlows => _waterFlows;
  WaterFlow[] _waterFlows;

  /// <summary>Water height bases indexed by the tile index.</summary>
  /// <remarks>
  /// <p>This height accounts both the terrain and the water obstacle blocks.</p>
  /// <p>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </p>
  /// </remarks>
  public int[] SurfaceHeights => _surfaceHeights;
  int[] _surfaceHeights;

  /// <summary>Indicates if the direct water system access can be used.</summary>
  public bool IsValid { get; private set; }

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

  #region ITickableSingleton implementation
  /// <summary>Updates stats in the water consumers and creates a thread safe copy.</summary>
  public void Tick() {
    var newMovers = new List<WaterMover>();
    foreach (var waterMover in _waterMovers) {
      if (waterMover.InputTileIndex == -1 && waterMover.OutputTileIndex == -1) {
        throw new InvalidOperationException("Water mover must have input or output defined");
      }
      if (waterMover.WaterFlow < 0) {
        throw new InvalidOperationException("Water flow must be a positive or zero value");
      }
      var threadSafeMover = waterMover.ThreadSafeWaterMover;
      if (threadSafeMover != null) {
        waterMover.WaterMoved += threadSafeMover.WaterMoved;
        threadSafeMover.WaterMoved = 0;
        if (waterMover.ConsumeWaterLimit >= 0) {
          threadSafeMover.ConsumeWaterLimit = Mathf.Max(waterMover.ConsumeWaterLimit - waterMover.WaterMoved, 0);
        }
        if (waterMover.DropWaterLimit >= 0) {
          threadSafeMover.DropWaterLimit = Mathf.Max(waterMover.DropWaterLimit - waterMover.WaterMoved, 0);
        }
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
  /// <remarks>
  /// This method is called from the water simulation threads at a very high frequency. Keep it simple and fast.
  /// </remarks>
  /// <param name="deltaTime">Simulation step delta.</param>
  void UpdateDepthsCallback(float deltaTime) {
    for (var i = _threadSafeWaterMovers.Count - 1; i >= 0; i--) {
      var waterMover = _threadSafeWaterMovers[i];
      var inputIndex = waterMover.InputTileIndex;
      var outputIndex = waterMover.OutputTileIndex;
      if (inputIndex != -1 && outputIndex != -1) {
        MoveWater(waterMover, deltaTime);
      } else if (inputIndex != -1) {
        ConsumeWater(waterMover, deltaTime);
      } else {
        DropWater(waterMover, deltaTime);
      }
    }
  }

  void MoveWater(WaterMover waterMover, float deltaTime) {
    var inputIndex = waterMover.InputTileIndex;
    var outputIndex = waterMover.OutputTileIndex;
    var inDepth = _waterDepths[inputIndex];
    var canMoveAmount = Mathf.Min(waterMover.WaterFlow * deltaTime, inDepth);
    if (canMoveAmount < float.Epsilon) {
      return;
    }
    var inWaterHeight = _surfaceHeights[inputIndex] + inDepth;
    var outWaterHeight = _surfaceHeights[outputIndex] + _waterDepths[outputIndex];
    if (waterMover.FreeFlow) {
      var waterDiff = (inWaterHeight - outWaterHeight) / 2;
      if (waterDiff < float.Epsilon) {
        return;
      }
      if (waterDiff < canMoveAmount) {
        canMoveAmount = waterDiff;
      }
    }
    if (waterMover.MinHeightAtInput > 0) {
      var waterToTarget = inWaterHeight - waterMover.MinHeightAtInput;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canMoveAmount) {
        canMoveAmount = waterToTarget;
      }
    }
    if (waterMover.MaxHeightAtOutput > 0) {
      var waterToTarget = waterMover.MaxHeightAtOutput - outWaterHeight;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canMoveAmount) {
        canMoveAmount = waterToTarget;
      }
    }
    var waterMovedTillNow = waterMover.WaterMoved;
    var consumeLimit = waterMover.ConsumeWaterLimit;
    if (consumeLimit >= 0) {
      var waterToTarget = consumeLimit - waterMovedTillNow;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canMoveAmount) {
        canMoveAmount = waterToTarget;
      }
    }
    var dropLimit = waterMover.DropWaterLimit;
    if (dropLimit >= 0) {
      var waterToTarget = dropLimit - waterMovedTillNow;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canMoveAmount) {
        canMoveAmount = waterToTarget;
      }
    }
    _waterDepths[inputIndex] -= canMoveAmount;
    _waterDepths[outputIndex] += canMoveAmount;
    waterMover.WaterMoved = waterMovedTillNow + canMoveAmount;
  }

  void ConsumeWater(WaterMover waterMover, float deltaTime) {
    var inputIndex = waterMover.InputTileIndex;
    var needAmount = waterMover.WaterFlow * deltaTime;
    var inDepth = _waterDepths[inputIndex];
    var canTakeAmount = Mathf.Min(needAmount, inDepth);
    if (canTakeAmount < float.Epsilon) {
      return;
    }
    if (waterMover.MinHeightAtInput > 0) {
      var waterToTarget = _surfaceHeights[inputIndex] + inDepth - waterMover.MinHeightAtInput;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canTakeAmount) {
        canTakeAmount = waterToTarget;
      }
    }
    var waterMovedTillNow = waterMover.WaterMoved;
    var consumeLimit = waterMover.ConsumeWaterLimit;
    if (consumeLimit >= 0) {
      var waterToTarget = consumeLimit - waterMovedTillNow;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canTakeAmount) {
        canTakeAmount = waterToTarget;
      }
    }
    _waterDepths[inputIndex] -= canTakeAmount;
    waterMover.WaterMoved = waterMovedTillNow + canTakeAmount;
  }

  void DropWater(WaterMover waterMover, float deltaTime) {
    var outputIndex = waterMover.OutputTileIndex;
    var canDropAmount = waterMover.WaterFlow * deltaTime;
    if (waterMover.MaxHeightAtOutput > 0) {
      var waterToTarget = waterMover.MaxHeightAtOutput - _surfaceHeights[outputIndex] - _waterDepths[outputIndex];
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canDropAmount) {
        canDropAmount = waterToTarget;
      }
    }
    var waterMovedTillNow = waterMover.WaterMoved;
    var dropLimit = waterMover.DropWaterLimit;
    if (dropLimit >= 0) {
      var waterToTarget = dropLimit - waterMovedTillNow;
      if (waterToTarget < float.Epsilon) {
        return;
      }
      if (waterToTarget < canDropAmount) {
        canDropAmount = waterToTarget;
      }
    }
    _waterDepths[outputIndex] += canDropAmount;
    waterMover.WaterMoved = waterMovedTillNow + canDropAmount;
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

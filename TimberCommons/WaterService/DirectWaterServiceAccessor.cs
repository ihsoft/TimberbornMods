// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using HarmonyLib;
using IgorZ.TimberDev.Utils;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WaterContaminationSystem;
using Timberborn.WaterSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService {

/// <summary>
/// Class that allows accessing to the internal water system logic. Be careful using it! The internal logic runs in
/// threads, so it may not be safe to access anything anytime.
/// </summary>
/// <remarks>
/// This code interacts with the internal game's water system objects and fields (via "publicize"). The changes to the
/// game logic can break the behavior.
/// </remarks>
public class DirectWaterServiceAccessor : IPostLoadableSingleton, ITickableSingleton {
  /// <summary>Water mover definition.</summary>
  /// <remarks>
  /// The mover takes water from the inout and drops it at the output. Various settings allow adjusting the exact
  /// behavior of the mover.
  /// </remarks>
  public class WaterMover {
    internal WaterMover ThreadSafeWaterMover;
    
    // ReSharper disable once MemberCanBePrivate.Global
    internal bool LogExtraStats;

    /// <summary>Index of the tile to get water from.</summary>
    /// <remarks>If set to <c>-1</c>, then the mover will only be adding water.</remarks>
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
    public bool FreeFlow = true;

    /// <summary>Tells that the contamination should also be moved.</summary>
    public bool MoveContaminatedWater;

    /// <summary>
    /// The maximum absolute water height to keep at the outtake. No water will be moved if the level is already high.
    /// </summary>
    /// <remarks>Set it to a negative value to indicate that this check is not needed.</remarks>
    /// <seealso cref="DirectWaterServiceAccessor.SurfaceHeights"/>
    public float MaxHeightAtOutput = -1;

    /// <summary>
    /// The minimum absolute water height to keep at the outtake. No water will be moved if the level is already high.
    /// </summary>
    /// <remarks>Set it to a negative value to indicate that this check is not needed.</remarks>
    /// <seealso cref="DirectWaterServiceAccessor.SurfaceHeights"/>
    public float MinHeightAtInput = -1;

    /// <inheritdoc/>
    public override string ToString() {
      // ReSharper disable once UseStringInterpolation
      return string.Format("[WaterMover#in={0},out={1},flow={2},free={3},inMin={4},outMax={5},moveBadWater={6}]",
                           InputTileIndex, OutputTileIndex, WaterFlow, FreeFlow, MinHeightAtInput, MaxHeightAtOutput,
                           MoveContaminatedWater);
    }

    internal WaterMover CopyDefinition() {
      return new WaterMover {
          InputTileIndex = InputTileIndex,
          OutputTileIndex = OutputTileIndex,
          DropWaterLimit = DropWaterLimit,
          ConsumeWaterLimit = ConsumeWaterLimit,
          WaterFlow = WaterFlow,
          FreeFlow = FreeFlow,
          MoveContaminatedWater = MoveContaminatedWater,
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
      MoveContaminatedWater = source.MoveContaminatedWater;
      MaxHeightAtOutput = source.MaxHeightAtOutput;
      MinHeightAtInput = source.MinHeightAtInput;
      LogExtraStats = source.LogExtraStats;
    }
  }

  readonly List<WaterMover> _waterMovers = new();
  List<WaterMover> _threadSafeWaterMovers = new();
  float[] _waterContaminations;
  float[] _contaminationsBuffer;

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

  /// <summary>Water height bases indexed by the tile index.</summary>
  /// <remarks>
  /// <p>This height accounts both the terrain and the water obstacle blocks.</p>
  /// <p>
  /// The values can be read from any thread, but the updates must be synchronized to the <c>ParallelTick</c> calls.
  /// </p>
  /// </remarks>
  public int[] SurfaceHeights => _surfaceHeights;
  int[] _surfaceHeights;

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
  /// <remarks>It's expected to be called after all the game state is loaded and ready.</remarks>
  public void PostLoad() {
    DebugEx.Fine("Initializing direct access to WaterSystem...");

    _waterDepths = _waterMap.WaterDepths;
    _waterContaminations = _waterContaminationMap.Contaminations;
    _contaminationsBuffer = _waterSimulator._contaminationsBuffer;
    _surfaceHeights = _impermeableSurfaceService.Heights;

    HarmonyPatcher.PatchRepeated(GetType().AssemblyQualifiedName, typeof(WaterSimulatorUpdateWaterParametersPatch));
    WaterSimulatorUpdateWaterParametersPatch.DirectWaterServiceAccessor = this;
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
  WaterMap _waterMap;
  WaterContaminationMap _waterContaminationMap;
  ImpermeableSurfaceService _impermeableSurfaceService;
  WaterSimulator _waterSimulator;

  /// <summary>Injects run-time dependencies.</summary>
  [Inject]
  public void InjectDependencies(WaterMap waterMap, WaterContaminationMap waterContaminationMap,
                                 ImpermeableSurfaceService impermeableSurfaceService, WaterSimulator waterSimulator) {
    _waterMap = waterMap;
    _waterContaminationMap = waterContaminationMap;
    _impermeableSurfaceService = impermeableSurfaceService;
    _waterSimulator = waterSimulator;
  }
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

    if (canMoveAmount < float.Epsilon) {
      return;  // Nothing to move.
    }

    _waterDepths[inputIndex] -= canMoveAmount;
    var initialWaterDepth = _waterDepths[outputIndex];
    var endWaterDepth = initialWaterDepth + canMoveAmount;
    _waterDepths[outputIndex] = endWaterDepth;

    var inputContamination = waterMover.MoveContaminatedWater ? _waterContaminations[inputIndex] : 0;
    var outputContamination =
        (_waterContaminations[outputIndex] * initialWaterDepth + inputContamination * canMoveAmount)
        / endWaterDepth;
    _contaminationsBuffer[outputIndex] = outputContamination > 1.0f ? 1.0f : outputContamination;

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

  #region Harmony patch to implement the custom updates to water depths
  [HarmonyPatch(typeof(WaterSimulator), nameof(WaterSimulator.UpdateWaterParameters))]
  static class WaterSimulatorUpdateWaterParametersPatch {
    public static DirectWaterServiceAccessor DirectWaterServiceAccessor;

    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once InconsistentNaming
    static void Postfix(float ____deltaTime) {
      DirectWaterServiceAccessor?.UpdateDepthsCallback(____deltaTime);
    }
  }
  #endregion
}

}

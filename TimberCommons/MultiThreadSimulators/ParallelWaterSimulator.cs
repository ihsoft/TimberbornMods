// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Timberborn.MapIndexSystem;
using Timberborn.WaterContaminationSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.MultiThreadSimulators {

/// <summary>
/// This class intercepts the stock game simulator calls and runs the logic via thread-pool to get a benefit of multiple
/// CPU cores.
/// </summary>
/// <remarks>
/// The original simulator code is reused as much as possible, but some methods were copied and changed to fit the
/// parallel computation logic.
/// </remarks>
sealed class ParallelWaterSimulator {
  const float MaxContamination = 1f;

  readonly WaterSimulator _instance;
  readonly WaterMap _waterMap;
  readonly WaterContaminationMap _waterContaminationMap;
  readonly ImpermeableSurfaceService _impermeableSurfaceService;
  readonly MapIndexService _mapIndexService;
  readonly WaterSimulationSettings _waterSimulationSettings;
  readonly WaterContaminationSimulationSettings _waterContaminationSimulationSettings;
  readonly ThreadSafeWaterEvaporationMap _threadSafeWaterEvaporationMap;

  readonly int _startingIndex;
  readonly int _stride;
  readonly int _tilesPerLine;
  readonly int _tilesPerColumn;

  readonly WaterFlow[] _tempOutflows;
  readonly float[] _initialWaterDepths;
  readonly float[] _contaminationsBuffer;
  readonly WaterContaminationDiffusion[] _contaminationDiffusions;
  readonly float _fixedDeltaTime;
  float _deltaTime;

  readonly CountdownEvent _processWaterDepthsEvent = new(0);
  readonly CountdownEvent _updateOutflowsEvent = new(0);
  readonly CountdownEvent _updateDiffusionEvent = new(0);
  readonly CountdownEvent _updateContaminationEvent = new(0);

  internal ParallelWaterSimulator(WaterSimulator instance) {
    _instance = instance;
    _waterMap = instance._waterMap;
    _waterContaminationMap = instance._waterContaminationMap;
    _impermeableSurfaceService = instance._impermeableSurfaceService;
    _mapIndexService = instance._mapIndexService;
    _waterSimulationSettings = instance._waterSimulationSettings;
    _waterContaminationSimulationSettings = instance._waterContaminationSimulationSettings;
    _threadSafeWaterEvaporationMap = instance._threadSafeWaterEvaporationMap;

    var mapIndexService = instance._mapIndexService;
    _startingIndex = mapIndexService.StartingIndex;
    _stride = mapIndexService.Stride;
    _tilesPerLine = mapIndexService.MapSize.x;
    _tilesPerColumn = mapIndexService.MapSize.y;

    _fixedDeltaTime = instance._fixedDeltaTime;
    _tempOutflows = instance._tempOutflows;
    _initialWaterDepths = instance._initialWaterDepths;
    _contaminationsBuffer = instance._contaminationsBuffer;
    _contaminationDiffusions = instance._contaminationDiffusions;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void ProcessSimulation() {
    _deltaTime = _fixedDeltaTime * _waterSimulationSettings.TimeScale;
    Array.Copy(_waterMap.WaterDepths, _initialWaterDepths, _initialWaterDepths.Length);
    Array.Copy(_waterContaminationMap.Contaminations, _contaminationsBuffer, _contaminationsBuffer.Length);
    
    UpdateOutflows();
    UpdateWaterParameters();
    SimulateContaminationDiffusion();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateOutflows() {
    _updateOutflowsEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            UpdateOutflowsChunk(indexCopy, indexCopy + _tilesPerLine);
            _updateOutflowsEvent.Signal();
          });
      index += _stride;
    }
    _updateOutflowsEvent.Wait();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateWaterParameters() {
    _processWaterDepthsEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            ProcessWaterDepthsChunk(indexCopy, indexCopy + _tilesPerLine);
            _processWaterDepthsEvent.Signal();
          });
      index += _stride;
    }
    _processWaterDepthsEvent.Wait();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void SimulateContaminationDiffusion() {
    _updateDiffusionEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            UpdateDiffusionChunk(indexCopy, indexCopy + _tilesPerLine);
            _updateDiffusionEvent.Signal();
          });
      index += _stride;
    }
    _updateDiffusionEvent.Wait();

    _updateContaminationEvent.Reset(_tilesPerColumn);
    index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            UpdateContaminationChunk(indexCopy, indexCopy + _tilesPerLine);
            _updateContaminationEvent.Signal();
          });
      index += _stride;
    }
    _updateContaminationEvent.Wait();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void ProcessWaterDepthsChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      var newDepth = _waterMap.WaterDepths[index] + _instance.ProcessWaterDepthChanges(index);
      _waterMap.WaterDepths[index] = newDepth > 0f ? newDepth : 0f;
      _instance.SimulateContaminationMovement(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateOutflowsChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      UpdateOutflows(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateDiffusionChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      _instance.UpdateDiffusion(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateContaminationChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      UpdateContamination(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateContamination(int index) {
    if (_waterMap.WaterDepths[index] > 0f) {
      var newContamination = _contaminationsBuffer[index] + _instance.GetContaminationDiffusionChange(index);
      _waterContaminationMap.Contaminations[index] =
          newContamination > MaxContamination ? MaxContamination : newContamination;
    } else {
      _waterContaminationMap.Contaminations[index] = 0f;
    }
  }

  /// <summary>A copy of the stock method for the purpose of patching (see <see cref="Outflow"/>.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateOutflows(int index) {
    var waterDepth = _waterMap.WaterDepths[index];
    if (waterDepth == 0f) {
      _tempOutflows[index] = default;
      return;
    }
    var bottomTarget = index - _stride;
    var leftTarget = index - 1;
    var topTarget = index + _stride;
    var rightTarget = index + 1;
    ref var oldOutflow = ref _waterMap.Outflows[index];
    var bottomFlow = Outflow(index, bottomTarget, oldOutflow.Bottom);
    var leftFlow = Outflow(index, leftTarget, oldOutflow.Left);
    var topFlow = Outflow(index, topTarget, oldOutflow.Top);
    var rightFlow = Outflow(index, rightTarget, oldOutflow.Right);
    var totalFlow = bottomFlow + leftFlow + topFlow + rightFlow;
    if (totalFlow == 0f) {
      _tempOutflows[index] = default;
      return;
    }
    var moveScaler = waterDepth / (totalFlow * _deltaTime);
    if (moveScaler > 1f) {
      moveScaler = 1f;
    }
    ref var tempOutflow = ref _tempOutflows[index];
    tempOutflow.Bottom = bottomFlow * moveScaler;
    tempOutflow.Left = leftFlow * moveScaler;
    tempOutflow.Top = topFlow * moveScaler;
    tempOutflow.Right = rightFlow * moveScaler;
  }

  /// <summary>A copy of the stock method for the purpose of patching. See the comments in the body.</summary>
  /// <remarks>If no patching were needed,w e could just call this method from the stock code.</remarks>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  float Outflow(int origin, int target, float oldOutflow) {
    var originSurfaceHeight = _impermeableSurfaceService.Heights[origin];
    var originWaterDepth = _waterMap.WaterDepths[origin];
    var originWaterHeight = originSurfaceHeight + originWaterDepth;
    var targetSurfaceHeight = _impermeableSurfaceService.Heights[target];
    var targetWaterDepth = _waterMap.WaterDepths[target];
    var targetWaterHeight = targetSurfaceHeight + targetWaterDepth;
    var waterHeightDiff = originWaterHeight - targetWaterHeight;
    var waterOverflow = originWaterHeight - targetSurfaceHeight;
    if (_impermeableSurfaceService.PartialObstacles[target]) {
      var softDamThreshold = _waterSimulationSettings.SoftDamThreshold;
      var midDamThreshold = _waterSimulationSettings.MidDamThreshold;
      if (waterOverflow < midDamThreshold) {
        var maxHardDamDecrease = _waterSimulationSettings.MaxHardDamDecrease;
        var hardDamThreshold = _waterSimulationSettings.HardDamThreshold;
        var num9 = waterOverflow < hardDamThreshold
            ? maxHardDamDecrease
            : Mathf.Lerp(
                0f, maxHardDamDecrease, 1f - Mathf.InverseLerp(hardDamThreshold, midDamThreshold, waterOverflow));
        var num10 = oldOutflow - num9;
        if (!(num10 < 0f)) {
          return num10;
        }
        return 0f;
      }
      if (waterOverflow < softDamThreshold && waterHeightDiff > 0f) {
        var num11 = softDamThreshold - midDamThreshold;
        var num12 = (waterOverflow - midDamThreshold) / num11;
        waterHeightDiff *= num12;
      }
    } else if (targetWaterDepth == 0f) {
      waterHeightDiff -= _waterSimulationSettings.WaterSpillThreshold;
    }
    var moveAmount = _deltaTime * _waterSimulationSettings.WaterFlowSpeed * waterHeightDiff;
    var num14 = oldOutflow + moveAmount;
    var flowSlowerOutflowPenaltyThreshold = _waterSimulationSettings.FlowSlowerOutflowPenaltyThreshold;
    if (num14 > flowSlowerOutflowPenaltyThreshold
        && originWaterHeight >= _impermeableSurfaceService.MinFlowSlowers[origin]) {
      var originBottom = origin - _stride;
      var originLeft = origin - 1;
      var originTop = origin + _stride;
      var originRight = origin + 1;

      // Here, the stock code gets outflows from _tempOutflows. Given this method is used to build _tempOutflows, it
      // seems very questionable as it results in building _tempOutflows from an incomplete state. It also prevents
      // parallelism, since the data being read is also being modified from the other workers. Based on all this, the
      // code was fixed to use Outflows instead (immutable copy during the stage execution). Tests didn't reveal any
      // obvious or noticeable artefacts.
      var top = _waterMap.Outflows[originBottom].Top;
      var right = _waterMap.Outflows[originLeft].Right;
      var bottom = _waterMap.Outflows[originTop].Bottom;
      var left = _waterMap.Outflows[originRight].Left;

      var num19 = (top + right + bottom + left) * _waterSimulationSettings.FlowSlowerOutflowMaxInflowPart;
      if (num14 > num19) {
        num14 -= _deltaTime * _waterSimulationSettings.FlowSlowerOutflowPenalty;
        if (num14 < flowSlowerOutflowPenaltyThreshold) {
          num14 = flowSlowerOutflowPenaltyThreshold;
        }
      }
    }
    var maxWaterfallOutflow = _waterSimulationSettings.MaxWaterfallOutflow;
    if (originSurfaceHeight > targetSurfaceHeight
        && num14 > maxWaterfallOutflow
        && _mapIndexService.IndexIsInActualMap(target)) {
      return maxWaterfallOutflow;
    }
    if (!(num14 < 0f)) {
      return num14;
    }
    return 0f;
  }
}

}

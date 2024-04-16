// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Timberborn.Common;
using Timberborn.MapIndexSystem;
using Timberborn.WaterContaminationSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterService {

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
      var newDepth = _waterMap.WaterDepths[index] + ProcessWaterDepthChanges(index);
      _waterMap.WaterDepths[index] = newDepth > 0f ? newDepth : 0f;
      SimulateContaminationMovement(index);
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
      UpdateDiffusion(index);
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
      var newContamination = _contaminationsBuffer[index] + GetContaminationDiffusionChange(index);
      _waterContaminationMap.Contaminations[index] =
          newContamination > MaxContamination ? MaxContamination : newContamination;
    } else {
      _waterContaminationMap.Contaminations[index] = 0f;
    }
  }

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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  float ProcessWaterDepthChanges(int index) {
    var inBottom = index - _stride;
    var inLeft = index - 1;
    var inTop = index + _stride;
    var inRight = index + 1;
    var top = _tempOutflows[inBottom].Top;
    var right = _tempOutflows[inLeft].Right;
    var bottom = _tempOutflows[inTop].Bottom;
    var left = _tempOutflows[inRight].Left;
    var inflowVol = top + right + bottom + left;
    ref var outflowTmp = ref _tempOutflows[index];
    var outflowVol = outflowTmp.Bottom + outflowTmp.Left + outflowTmp.Top + outflowTmp.Right;
    var addVol = inflowVol - outflowVol;
    var baseEvaporation = _waterMap.WaterDepths[index] < _waterSimulationSettings.FastEvaporationDepthThreshold
        ? _waterSimulationSettings.FastEvaporationSpeed
        : _waterSimulationSettings.NormalEvaporationSpeed;
    var evaporationAdj = _threadSafeWaterEvaporationMap.EvaporationModifiers[index];
    var evaporation = baseEvaporation * evaporationAdj;
    var result = (addVol - evaporation) * _deltaTime;
    ref var reference2 = ref _waterMap.Outflows[index];
    var outflowBalancingScaler = _waterSimulationSettings.OutflowBalancingScaler;
    var num11 = outflowTmp.Bottom - top * outflowBalancingScaler;
    var num12 = outflowTmp.Left - right * outflowBalancingScaler;
    var num13 = outflowTmp.Top - bottom * outflowBalancingScaler;
    var num14 = outflowTmp.Right - left * outflowBalancingScaler;
    reference2.Bottom = num11 > 0f ? num11 : 0f;
    reference2.Left = num12 > 0f ? num12 : 0f;
    reference2.Top = num13 > 0f ? num13 : 0f;
    reference2.Right = num14 > 0f ? num14 : 0f;
    return result;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void SimulateContaminationMovement(int index) {
    var num = index - _stride;
    var num2 = index - 1;
    var num3 = index + _stride;
    var num4 = index + 1;
    ref var reference = ref _tempOutflows[index];
    var num5 = (_tempOutflows[num].Top - reference.Bottom) * _deltaTime;
    var num6 = (_tempOutflows[num2].Right - reference.Left) * _deltaTime;
    var num7 = (_tempOutflows[num3].Bottom - reference.Top) * _deltaTime;
    var num8 = (_tempOutflows[num4].Left - reference.Right) * _deltaTime;
    var num9 = 0f;
    var num10 = 0f;
    var num11 = 0f;
    if (num5 > 0f) {
      num11 = _waterContaminationMap.Contaminations[num];
      num9 += num5;
    } else {
      num10 += num5;
    }
    var num12 = 0f;
    if (num6 > 0f) {
      num12 = _waterContaminationMap.Contaminations[num2];
      num9 += num6;
    } else {
      num10 += num6;
    }
    var num13 = 0f;
    if (num7 > 0f) {
      num13 = _waterContaminationMap.Contaminations[num3];
      num9 += num7;
    } else {
      num10 += num7;
    }
    var num14 = 0f;
    if (num8 > 0f) {
      num14 = _waterContaminationMap.Contaminations[num4];
      num9 += num8;
    } else {
      num10 += num8;
    }
    if (num9 > 0f) {
      var num15 = _initialWaterDepths[index];
      var num16 = num15 + num6 + num7 + num8 + num5;
      if (num16 != 0f) {
        var value = (_waterContaminationMap.Contaminations[index] * (num15 + num10)
                + num11 * num5
                + num12 * num6
                + num13 * num7
                + num14 * num8)
            / num16;
        _contaminationsBuffer[index] = FastMath.Clamp(value, 0f, MaxContamination);
      }
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateDiffusion(int index) {
    ref var reference = ref _contaminationDiffusions[index];
    reference.DiffusionFraction = 0f;
    if (!_impermeableSurfaceService.PartialObstacles[index]) {
      var num = index - _stride;
      var num2 = index - 1;
      var num3 = index + _stride;
      var num4 = index + 1;
      ref var reference2 = ref _tempOutflows[index];
      var outflowToTarget = reference2.Bottom - _tempOutflows[num].Top;
      var outflowToTarget2 = reference2.Left - _tempOutflows[num2].Right;
      var outflowToTarget3 = reference2.Top - _tempOutflows[num3].Bottom;
      var outflowToTarget4 = reference2.Right - _tempOutflows[num4].Left;
      var sourceWaterHeight = _waterMap.WaterDepths[index] + _impermeableSurfaceService.Heights[index];
      var flag = _instance.CanDiffuse(sourceWaterHeight, num, outflowToTarget);
      var flag2 = _instance.CanDiffuse(sourceWaterHeight, num2, outflowToTarget2);
      var flag3 = _instance.CanDiffuse(sourceWaterHeight, num3, outflowToTarget3);
      var flag4 = _instance.CanDiffuse(sourceWaterHeight, num4, outflowToTarget4);
      if (flag || flag2 || flag3 || flag4) {
        reference.CanDiffuseBottom = flag;
        reference.CanDiffuseLeft = flag2;
        reference.CanDiffuseTop = flag3;
        reference.CanDiffuseRight = flag4;
        var num5 = (flag ? 1 : 0) + (flag2 ? 1 : 0) + (flag3 ? 1 : 0) + (flag4 ? 1 : 0);
        reference.DiffusionFraction = 1f / num5;
      }
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  float GetContaminationDiffusionChange(int index) {
    var waterContaminationDiffusion = _contaminationDiffusions[index];
    if (waterContaminationDiffusion.DiffusionFraction > 0f) {
      var targetIndex = index - _stride;
      var targetIndex2 = index - 1;
      var targetIndex3 = index + _stride;
      var targetIndex4 = index + 1;
      var sourceContamination = _contaminationsBuffer[index];
      var sourceWaterDepth = _waterMap.WaterDepths[index];
      var num = 0f;
      var diffusionFraction = waterContaminationDiffusion.DiffusionFraction;
      if (waterContaminationDiffusion.CanDiffuseBottom) {
        num += CalculateDiffusion(sourceContamination, sourceWaterDepth, targetIndex, diffusionFraction);
      }
      if (waterContaminationDiffusion.CanDiffuseLeft) {
        num += CalculateDiffusion(sourceContamination, sourceWaterDepth, targetIndex2, diffusionFraction);
      }
      if (waterContaminationDiffusion.CanDiffuseTop) {
        num += CalculateDiffusion(sourceContamination, sourceWaterDepth, targetIndex3, diffusionFraction);
      }
      if (waterContaminationDiffusion.CanDiffuseRight) {
        num += CalculateDiffusion(sourceContamination, sourceWaterDepth, targetIndex4, diffusionFraction);
      }
      return num * _deltaTime;
    }
    return 0f;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  float CalculateDiffusion(float sourceContamination, float sourceWaterDepth, int targetIndex,
                           float diffusionFraction) {
    var waterContaminationDiffusion = _contaminationDiffusions[targetIndex];
    var num = _contaminationsBuffer[targetIndex];
    var num2 = _waterMap.WaterDepths[targetIndex];
    var num3 = num - sourceContamination;
    float num5;
    if (num3 > 0f) {
      var num4 = waterContaminationDiffusion.DiffusionFraction * num;
      num5 = num3 < num4 ? num3 : num4;
    } else {
      var num6 = (0f - diffusionFraction) * sourceContamination;
      num5 = num3 > num6 ? num3 : num6;
    }
    return num2 / (sourceWaterDepth + num2) * num5 * _waterContaminationSimulationSettings.DiffusionRate;
  }
}

}

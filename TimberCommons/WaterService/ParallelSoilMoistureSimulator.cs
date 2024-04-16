// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Timberborn.SoilMoistureSystem;
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
sealed class ParallelSoilMoistureSimulator {
  readonly SoilMoistureSimulator _instance;
  readonly IWaterService _waterService;
  readonly SoilMoistureSimulationSettings _soilMoistureSimulationSettings;

  readonly int _startingIndex;
  readonly int _stride;
  readonly int _tilesPerLine;
  readonly int _tilesPerColumn;

  // ReSharper disable once InconsistentNaming
  readonly float[] MoistureLevels;
  readonly List<Vector2Int> _moistureLevelsChangedLastTick;
  readonly float[] _lastTickMoistureLevels;
  readonly int[] _wateredNeighbours;
  readonly int[] _clusterSaturation;

  readonly CountdownEvent _countWateredNeighborsEvent = new(0);
  readonly CountdownEvent _calculateClusterSaturationAndWaterEvaporationEvent = new(0);
  readonly CountdownEvent _calculateMoistureEvent = new(0);

  internal ParallelSoilMoistureSimulator(SoilMoistureSimulator instance) {
    _instance = instance;
    _waterService = instance._waterService;
    _soilMoistureSimulationSettings = instance._soilMoistureSimulationSettings;

    var mapIndexService = instance._mapIndexService;
    _startingIndex = mapIndexService.StartingIndex;
    _stride = mapIndexService.Stride;
    _tilesPerLine = mapIndexService.MapSize.x;
    _tilesPerColumn = mapIndexService.MapSize.y;

    MoistureLevels = instance.MoistureLevels;
    _moistureLevelsChangedLastTick = instance._moistureLevelsChangedLastTick;
    _lastTickMoistureLevels = instance._lastTickMoistureLevels;
    _wateredNeighbours = instance._wateredNeighbours;
    _clusterSaturation = instance._clusterSaturation;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void TickSimulation() {
    Array.Copy(MoistureLevels, _lastTickMoistureLevels, _instance.MoistureLevels.Length);
    CountWateredNeighbors();
    CalculateClusterSaturationAndWaterEvaporation();
    CalculateMoisture();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CountWateredNeighbors() {
    _countWateredNeighborsEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            CountWateredNeighborsChunk(indexCopy, indexCopy + _tilesPerLine);
            _countWateredNeighborsEvent.Signal();
          });
      index += _stride;
    }
    _countWateredNeighborsEvent.Wait();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateClusterSaturationAndWaterEvaporation() {
    _calculateClusterSaturationAndWaterEvaporationEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            CalculateClusterSaturationAndWaterEvaporationChunk(indexCopy, indexCopy + _tilesPerLine);
            _calculateClusterSaturationAndWaterEvaporationEvent.Signal();
          });
      index += _stride;
    }
    _calculateClusterSaturationAndWaterEvaporationEvent.Wait();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateMoisture() {
    _calculateMoistureEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            CalculateMoistureChunk(indexCopy, indexCopy + _tilesPerLine);
            _calculateMoistureEvent.Signal();
          });
      index += _stride;
    }
    _calculateMoistureEvent.Wait();

    index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      for (var j = 0; j < _tilesPerLine; j++) {
        if (Mathf.Abs(MoistureLevels[index] - _lastTickMoistureLevels[index]) > float.Epsilon) {
          _moistureLevelsChangedLastTick.Add(new Vector2Int(j, i));
        }
        index++;
      }
      index += 2;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CountWateredNeighborsChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      CountWateredNeighbors(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateClusterSaturationAndWaterEvaporationChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      CalculateClusterSaturationAndWaterEvaporation(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateMoistureChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      MoistureLevels[index] = _instance.GetUpdatedMoisture(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CountWateredNeighbors(int index) {
    var count = 0;
    if (_waterService.WaterDepth(index) > 0f) {
      count++;
      var num = index - _stride;
      var num2 = index - _stride - 1;
      var num3 = index - 1;
      var num4 = index + _stride - 1;
      var num5 = index + _stride;
      var num6 = index + _stride + 1;
      var num7 = index + 1;
      var num8 = index - _stride + 1;
      count += _waterService.WaterDepth(num) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num2) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num3) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num4) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num5) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num6) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num7) > 0f ? 1 : 0;
      count += _waterService.WaterDepth(num8) > 0f ? 1 : 0;
    }
    _wateredNeighbours[index] = count;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateClusterSaturationAndWaterEvaporation(int index) {
    if (!(_waterService.WaterDepth(index) > 0f)) {
      _clusterSaturation[index] = 0;
      _waterService.SetWaterEvaporationModifier(index, 1f);
      return;
    }
    var num2 = index - _stride;
    var num3 = index - 1;
    var num4 = index + _stride;
    var num5 = index + 1;
    var num6 = _wateredNeighbours[index];
    var num7 = _wateredNeighbours[num2];
    var num8 = _wateredNeighbours[num3];
    var num9 = _wateredNeighbours[num4];
    var num10 = _wateredNeighbours[num5];
    if (num7 > num6) {
      num6 = num7 - 1;
    }
    if (num8 > num6) {
      num6 = num8 - 1;
    }
    if (num9 > num6) {
      num6 = num9 - 1;
    }
    if (num10 > num6) {
      num6 = num10 - 1;
    }
    _clusterSaturation[index] = num6 > _soilMoistureSimulationSettings.MaxClusterSaturation
        ? _soilMoistureSimulationSettings.MaxClusterSaturation
        : num6;
    var num11 = 10 - num6;
    var modifier = _soilMoistureSimulationSettings.QuadraticEvaporationCoefficient * num11 * num11
        + _soilMoistureSimulationSettings.LinearQuadraticCoefficient * num11
        + _soilMoistureSimulationSettings.ConstantQuadraticCoefficient;
    _waterService.SetWaterEvaporationModifier(index, modifier);
  }
}

}

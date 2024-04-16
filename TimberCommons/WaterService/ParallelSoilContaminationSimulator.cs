// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Timberborn.SoilContaminationSystem;
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
sealed class ParallelSoilContaminationSimulator {

  readonly SoilContaminationSimulator _instance;
  readonly SoilContaminationSimulationSettings _soilContaminationSimulationSettings;

  readonly int _startingIndex;
  readonly int _stride;
  readonly int _tilesPerLine;
  readonly int _tilesPerColumn;

  // ReSharper disable once InconsistentNaming
  readonly float[] ContaminationLevels;
  readonly float[] _contaminationCandidates;
  readonly float[] _lastTickContaminationCandidates;
  readonly List<Vector2Int> _contaminationsChangedLastTick;
  readonly bool[] _changedLevels;

  readonly CountdownEvent _calculateContaminationCandidatesEvent = new(0);
  readonly CountdownEvent _updateContaminationsFromCandidatesEvent = new(0);

  internal ParallelSoilContaminationSimulator(SoilContaminationSimulator instance) {
    _instance = instance;
    _soilContaminationSimulationSettings = instance._soilContaminationSimulationSettings;

    var mapIndexService = instance._mapIndexService;
    _startingIndex = mapIndexService.StartingIndex;
    _stride = mapIndexService.Stride;
    _tilesPerLine = mapIndexService.MapSize.x;
    _tilesPerColumn = mapIndexService.MapSize.y;

    ContaminationLevels = instance.ContaminationLevels;
    _contaminationCandidates = instance._contaminationCandidates;
    _lastTickContaminationCandidates = instance._lastTickContaminationCandidates;
    _contaminationsChangedLastTick = instance._contaminationsChangedLastTick;
    _changedLevels = new bool[ContaminationLevels.Length];
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal void TickSimulation() {
    Array.Copy(_contaminationCandidates, _lastTickContaminationCandidates, _contaminationCandidates.Length);
    CalculateContaminationCandidates();
    UpdateContaminationsFromCandidates();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateContaminationCandidates() {
    _calculateContaminationCandidatesEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            CalculateContaminationCandidatesChunk(indexCopy, indexCopy + _tilesPerLine);
            _calculateContaminationCandidatesEvent.Signal();
          });
      index += _stride;
    }
    _calculateContaminationCandidatesEvent.Wait();
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateContaminationsFromCandidates() {
    Array.Clear(_changedLevels, 0, _changedLevels.Length);

    _updateContaminationsFromCandidatesEvent.Reset(_tilesPerColumn);
    var index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      var indexCopy = index;
      ThreadPool.QueueUserWorkItem(
          _ => {
            UpdateContaminationsFromCandidatesChunk(indexCopy, indexCopy + _tilesPerLine);
            _updateContaminationsFromCandidatesEvent.Signal();
          });
      index += _stride;
    }
    _updateContaminationsFromCandidatesEvent.Wait();

    index = _startingIndex;
    for (var i = 0; i < _tilesPerColumn; i++) {
      for (var j = 0; j < _tilesPerLine; j++) {
        if (_changedLevels[index]) {
          _contaminationsChangedLastTick.Add(new Vector2Int(j, i));
        }
        index++;
      }
      index += 2;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateContaminationCandidatesChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      CalculateContaminationCandidates(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void CalculateContaminationCandidates(int index) {
    _contaminationCandidates[index] = _instance.GetContaminationCandidate(index);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateContaminationsFromCandidatesChunk(int start, int end) {
    for (var index = start; index < end; index++) {
      UpdateContaminationsFromCandidates(index);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void UpdateContaminationsFromCandidates(int index) {
    var oldLevel = ContaminationLevels[index];
    var num3 = _contaminationCandidates[index];
    var num4 = num3 - oldLevel;
    var num5 = num4 > 0f
        ? _soilContaminationSimulationSettings.ContaminationPositiveEqualizationRate
        : _soilContaminationSimulationSettings.ContaminationNegativeEqualizationRate;
    var num6 = _instance._deltaTime * num5;
    var newLevel = num4 <= num6 && num4 >= 0f - num6 ? num3 : oldLevel + Math.Sign(num4) * num6;
    if (newLevel < SoilContaminationSimulator.MinimumSoilContamination) {
      newLevel = 0f;
    }
    // FIXME: Consider using a floating point friendly comparision.
    if (newLevel != oldLevel) {
      ContaminationLevels[index] = newLevel;
      _changedLevels[index] = true;
    }
  }
}

}

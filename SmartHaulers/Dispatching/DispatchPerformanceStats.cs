// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics;
using UnityEngine;

namespace IgorZ.SmartHaulers.Dispatching;

sealed class DispatchPerformanceStats {
  const int WindowSize = 100;

  readonly DispatchPerformanceSample[] _samples = new DispatchPerformanceSample[WindowSize];

  Stopwatch _refreshStopwatch;
  long _agentTicks;
  long _activeOrderTicks;
  long _queuedOrderTicks;
  long _constructionOrderTicks;
  long _readinessTicks;
  long _decisionTicks;
  long _sortTicks;
  long _pickupPathTicks;
  long _deliveryPathTicks;
  int _pickupPathCalls;
  int _deliveryPathCalls;
  int _nextSampleIndex;
  int _currentFrame = -1;
  int _currentSampleIndex = -1;
  int _sampleCount;
  DispatchPerformanceSample _lastSample;

  public DispatchPerformanceSample LastSample => _lastSample;
  public DispatchPerformanceSample WindowTotal {
    get {
      var sum = default(DispatchPerformanceSample);
      for (var i = 0; i < _sampleCount; i++) {
        sum = sum.Add(_samples[i]);
      }
      return sum;
    }
  }

  public DispatchPerformanceSample WindowAverage {
    get {
      if (_sampleCount == 0) {
        return default;
      }
      var sum = WindowTotal;
      return new DispatchPerformanceSample(
          sum.TotalTicks / _sampleCount,
          sum.AgentTicks / _sampleCount,
          sum.ActiveOrderTicks / _sampleCount,
          sum.QueuedOrderTicks / _sampleCount,
          sum.ConstructionOrderTicks / _sampleCount,
          sum.ReadinessTicks / _sampleCount,
          sum.DecisionTicks / _sampleCount,
          sum.SortTicks / _sampleCount,
          sum.PickupPathTicks / _sampleCount,
          sum.DeliveryPathTicks / _sampleCount,
          sum.PickupPathCalls / _sampleCount,
          sum.DeliveryPathCalls / _sampleCount);
    }
  }

  public int WindowSampleCount => _sampleCount;

  public void BeginRefresh() {
    _agentTicks = 0;
    _activeOrderTicks = 0;
    _queuedOrderTicks = 0;
    _constructionOrderTicks = 0;
    _readinessTicks = 0;
    _decisionTicks = 0;
    _sortTicks = 0;
    _pickupPathTicks = 0;
    _deliveryPathTicks = 0;
    _pickupPathCalls = 0;
    _deliveryPathCalls = 0;
    _refreshStopwatch = Stopwatch.StartNew();
  }

  public void EndRefresh() {
    if (_refreshStopwatch == null) {
      return;
    }
    _refreshStopwatch.Stop();
    _lastSample = new DispatchPerformanceSample(
        _refreshStopwatch.ElapsedTicks,
        _agentTicks,
        _activeOrderTicks,
        _queuedOrderTicks,
        _constructionOrderTicks,
        _readinessTicks,
        _decisionTicks,
        _sortTicks,
        _pickupPathTicks,
        _deliveryPathTicks,
        _pickupPathCalls,
        _deliveryPathCalls);
    AddToFrameSample(Time.frameCount, _lastSample);
    _refreshStopwatch = null;
  }

  void AddToFrameSample(int frame, DispatchPerformanceSample sample) {
    if (_currentFrame != frame || _currentSampleIndex < 0) {
      _currentFrame = frame;
      _currentSampleIndex = _nextSampleIndex;
      _samples[_currentSampleIndex] = sample;
      _nextSampleIndex = (_nextSampleIndex + 1) % WindowSize;
      _sampleCount = Math.Min(_sampleCount + 1, WindowSize);
    } else {
      _samples[_currentSampleIndex] = _samples[_currentSampleIndex].Add(sample);
    }
    _lastSample = _samples[_currentSampleIndex];
  }

  public void BeginPickupPath() {
    _pickupPathCalls++;
  }

  public void EndPickupPath(long startTimestamp) {
    _pickupPathTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void BeginDeliveryPath() {
    _deliveryPathCalls++;
  }

  public void EndDeliveryPath(long startTimestamp) {
    _deliveryPathTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public long BeginSection() {
    return Stopwatch.GetTimestamp();
  }

  public void EndAgentSection(long startTimestamp) {
    _agentTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void EndActiveOrderSection(long startTimestamp) {
    _activeOrderTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void EndQueuedOrderSection(long startTimestamp) {
    _queuedOrderTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void EndConstructionOrderSection(long startTimestamp) {
    _constructionOrderTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void EndReadinessSection(long startTimestamp) {
    _readinessTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void EndDecisionSection(long startTimestamp) {
    _decisionTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public void EndSortSection(long startTimestamp) {
    _sortTicks += Stopwatch.GetTimestamp() - startTimestamp;
  }

  public static long Timestamp() {
    return Stopwatch.GetTimestamp();
  }

  public T MeasurePickupPath<T>(Func<T> action) {
    var start = Stopwatch.GetTimestamp();
    try {
      return action();
    } finally {
      _pickupPathTicks += Stopwatch.GetTimestamp() - start;
      _pickupPathCalls++;
    }
  }

  public T MeasureDeliveryPath<T>(Func<T> action) {
    var start = Stopwatch.GetTimestamp();
    try {
      return action();
    } finally {
      _deliveryPathTicks += Stopwatch.GetTimestamp() - start;
      _deliveryPathCalls++;
    }
  }

  public static double TicksToMilliseconds(long ticks) {
    return ticks * 1000.0 / Stopwatch.Frequency;
  }
}

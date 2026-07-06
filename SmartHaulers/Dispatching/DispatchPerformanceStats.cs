// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics;

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
  long _activeRoutePathTicks;
  long _plannerRoutePathTicks;
  long _decisionRoutePathTicks;
  long _decisionPickupPathTicks;
  long _remainingPathTicks;
  long _remainingPickupPathTicks;
  long _remainingDeliveryPathTicks;
  int _agentCount;
  int _activeOrderCount;
  int _queuedOrderCount;
  int _constructionOrderCount;
  int _decisionOrderCount;
  int _decisionCandidateCount;
  int _pickupPathCalls;
  int _deliveryPathCalls;
  int _activeRoutePathCalls;
  int _plannerRoutePathCalls;
  int _decisionRoutePathCalls;
  int _decisionPickupPathCalls;
  int _remainingPathCalls;
  int _remainingPickupPathCalls;
  int _remainingDeliveryPathCalls;
  int _routeCacheHits;
  int _routeCacheMisses;
  int _routeCacheClears;
  int _nextSampleIndex;
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
          sum.ActiveRoutePathTicks / _sampleCount,
          sum.PlannerRoutePathTicks / _sampleCount,
          sum.DecisionRoutePathTicks / _sampleCount,
          sum.DecisionPickupPathTicks / _sampleCount,
          sum.RemainingPathTicks / _sampleCount,
          sum.RemainingPickupPathTicks / _sampleCount,
          sum.RemainingDeliveryPathTicks / _sampleCount,
          sum.AgentCount / _sampleCount,
          sum.ActiveOrderCount / _sampleCount,
          sum.QueuedOrderCount / _sampleCount,
          sum.ConstructionOrderCount / _sampleCount,
          sum.DecisionOrderCount / _sampleCount,
          sum.DecisionCandidateCount / _sampleCount,
          sum.PickupPathCalls / _sampleCount,
          sum.DeliveryPathCalls / _sampleCount,
          sum.ActiveRoutePathCalls / _sampleCount,
          sum.PlannerRoutePathCalls / _sampleCount,
          sum.DecisionRoutePathCalls / _sampleCount,
          sum.DecisionPickupPathCalls / _sampleCount,
          sum.RemainingPathCalls / _sampleCount,
          sum.RemainingPickupPathCalls / _sampleCount,
          sum.RemainingDeliveryPathCalls / _sampleCount,
          sum.RouteCacheHits / _sampleCount,
          sum.RouteCacheMisses / _sampleCount,
          sum.RouteCacheClears / _sampleCount);
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
    _activeRoutePathTicks = 0;
    _plannerRoutePathTicks = 0;
    _decisionRoutePathTicks = 0;
    _decisionPickupPathTicks = 0;
    _remainingPathTicks = 0;
    _remainingPickupPathTicks = 0;
    _remainingDeliveryPathTicks = 0;
    _agentCount = 0;
    _activeOrderCount = 0;
    _queuedOrderCount = 0;
    _constructionOrderCount = 0;
    _decisionOrderCount = 0;
    _decisionCandidateCount = 0;
    _pickupPathCalls = 0;
    _deliveryPathCalls = 0;
    _activeRoutePathCalls = 0;
    _plannerRoutePathCalls = 0;
    _decisionRoutePathCalls = 0;
    _decisionPickupPathCalls = 0;
    _remainingPathCalls = 0;
    _remainingPickupPathCalls = 0;
    _remainingDeliveryPathCalls = 0;
    _routeCacheHits = 0;
    _routeCacheMisses = 0;
    _routeCacheClears = 0;
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
        _activeRoutePathTicks,
        _plannerRoutePathTicks,
        _decisionRoutePathTicks,
        _decisionPickupPathTicks,
        _remainingPathTicks,
        _remainingPickupPathTicks,
        _remainingDeliveryPathTicks,
        _agentCount,
        _activeOrderCount,
        _queuedOrderCount,
        _constructionOrderCount,
        _decisionOrderCount,
        _decisionCandidateCount,
        _pickupPathCalls,
        _deliveryPathCalls,
        _activeRoutePathCalls,
        _plannerRoutePathCalls,
        _decisionRoutePathCalls,
        _decisionPickupPathCalls,
        _remainingPathCalls,
        _remainingPickupPathCalls,
        _remainingDeliveryPathCalls,
        _routeCacheHits,
        _routeCacheMisses,
        _routeCacheClears);
    AddSample(_lastSample);
    _refreshStopwatch = null;
  }

  void AddSample(DispatchPerformanceSample sample) {
    _samples[_nextSampleIndex] = sample;
    _nextSampleIndex = (_nextSampleIndex + 1) % WindowSize;
    _sampleCount = Math.Min(_sampleCount + 1, WindowSize);
    _lastSample = sample;
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

  public void CountAgent() {
    _agentCount++;
  }

  public void CountActiveOrder() {
    _activeOrderCount++;
  }

  public void CountQueuedOrder() {
    _queuedOrderCount++;
  }

  public void CountConstructionOrder() {
    _constructionOrderCount++;
  }

  public void CountDecisionOrder() {
    _decisionOrderCount++;
  }

  public void CountDecisionCandidate() {
    _decisionCandidateCount++;
  }

  public void CountRouteCacheHit() {
    _routeCacheHits++;
  }

  public void CountRouteCacheMiss() {
    _routeCacheMisses++;
  }

  public void CountRouteCacheClear() {
    _routeCacheClears++;
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

  public void EndActiveRoutePath(long startTimestamp) {
    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
    _deliveryPathTicks += ticks;
    _activeRoutePathTicks += ticks;
    _deliveryPathCalls++;
    _activeRoutePathCalls++;
  }

  public void EndPlannerRoutePath(long startTimestamp) {
    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
    _deliveryPathTicks += ticks;
    _plannerRoutePathTicks += ticks;
    _deliveryPathCalls++;
    _plannerRoutePathCalls++;
  }

  public void EndDecisionRoutePath(long startTimestamp) {
    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
    _deliveryPathTicks += ticks;
    _decisionRoutePathTicks += ticks;
    _deliveryPathCalls++;
    _decisionRoutePathCalls++;
  }

  public void EndDecisionPickupPath(long startTimestamp) {
    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
    _pickupPathTicks += ticks;
    _decisionPickupPathTicks += ticks;
    _pickupPathCalls++;
    _decisionPickupPathCalls++;
  }

  public void EndRemainingPath(long startTimestamp, bool isPickup) {
    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
    if (isPickup) {
      _pickupPathTicks += ticks;
      _remainingPickupPathTicks += ticks;
      _remainingPickupPathCalls++;
    } else {
      _deliveryPathTicks += ticks;
      _remainingDeliveryPathTicks += ticks;
      _remainingDeliveryPathCalls++;
    }
    _remainingPathTicks += ticks;
    if (isPickup) {
      _pickupPathCalls++;
    } else {
      _deliveryPathCalls++;
    }
    _remainingPathCalls++;
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

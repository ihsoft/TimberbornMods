// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Runtime.CompilerServices;

namespace IgorZ.SmartPower.Utils;

/// <summary>Helper to execute an action after a certain number of ticks if the condition still stays.</summary>
public class TickDelayedAction {
  readonly int _skipTicks;
  readonly Func<int> _tickProvider;

  int _startedTick = int.MinValue;
  int _lastTick = int.MinValue;

  /// <summary>The tick number of when the action countdown has started.</summary>
  /// <value>A negative value if the countdown is not started.</value>
  public int StartedTick => _startedTick;

  /// <summary>Creates a new instance of the delayed action.</summary>
  /// <param name="skipTicks">Number of ticks to wait before executing the action.</param>
  /// <param name="tickProvider">
  /// Function to get the current tick number. The tick number must be monotonically increasing.
  /// </param>
  public TickDelayedAction(int skipTicks, Func<int> tickProvider) {
    _skipTicks = skipTicks;
    _tickProvider = tickProvider;
  }

  /// <summary>Tries executing the action in the current tick.</summary>
  /// <remarks>
  /// The action will only execute if the specified number of ticks has passed. This method must be called at least once
  /// per tick to maintain the sequence. If any tick was skipped, the timer will reset.
  /// </remarks>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool Execute(Action action) {
    var currentTick = _tickProvider();
    if (_skipTicks == 0 || _startedTick + _skipTicks == currentTick) {
      _lastTick = int.MinValue;
      _startedTick = int.MinValue;
      action();
      return true;
    }
    if (_lastTick + 1 != currentTick) {
      _startedTick = currentTick;
    }
    _lastTick = currentTick;
    return false;
  }
}

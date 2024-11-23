// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Runtime.CompilerServices;

namespace IgorZ.SmartPower.Utils;

/// <summary>
/// Helper to execute an action with a delay if the condition stays for the specified number of ticks.
/// </summary>
/// <remarks>
/// Each call to <see cref="Execute"/> is recognized as the "condition met" case. The calls from the sequential ticks
/// decrease the countdown and eventually trigger the action. If the countdown is not decreased in the next tick, then
/// the countdown is restarted. The countdown can be explicitly restarted via <see cref="Reset"/>.
/// </remarks>
public class TickDelayedAction {
  readonly int _skipTicks;
  readonly Func<int> _tickProvider;

  int _lastTick = int.MinValue;
  int _ticksLeft;

  /// <summary>Number of ticks left before the action will be executed.</summary>
  /// <value>-1 if action has expired or wasn't started.</value>
  /// <seealso cref="IsTicking"/>
  public int TicksLeft => _ticksLeft;

  /// <summary>Creates a new instance of the delayed action.</summary>
  /// <param name="skipTicks">Number of ticks to wait before executing the action.</param>
  /// <param name="tickProvider">
  /// Function to get the current tick number. The tick number must be monotonically increasing.
  /// </param>
  public TickDelayedAction(int skipTicks, Func<int> tickProvider) {
    _skipTicks = skipTicks;
    _tickProvider = tickProvider;
  }

  /// <summary>Decreases the countdown and executes the action if the countdown becomes zero.</summary>
  /// <remarks>
  /// The action must be called in each tick to decrease the countdown. If this method wasn't called in the last tick,
  /// then the countdown is restarted. It is safe to call this method multiple times in the same tick.
  /// </remarks>
  /// <seealso cref="TicksLeft"/>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool Execute(Action action, bool force = false) {
    if (_skipTicks == 0 || force) {
      action();
      Reset();
      return true;
    }

    var currentTick = _tickProvider();
    if (_lastTick + 1 == currentTick) {
      _ticksLeft--;
      if (_ticksLeft == 0) {
        action();
        Reset();
        return true;
      }
    } else if (_lastTick != currentTick) {
      _ticksLeft = _skipTicks;
    }
    _lastTick = currentTick;
    return false;
  }

  /// <summary>Resets the action countdown.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Reset() {
    _lastTick = int.MinValue;
    _ticksLeft = -1;
  }

  /// <summary>Tells whether the action countdown is being ticking.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool IsTicking() => _skipTicks > 0 && _lastTick >= 0 && _tickProvider() - _lastTick <= 1;
}

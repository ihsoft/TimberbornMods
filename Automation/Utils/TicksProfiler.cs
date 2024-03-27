// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics;
using System.Text;

namespace IgorZ.TimberDev.Utils {

/// <summary>Helper class to profile execution timings.</summary>
public sealed class TicksProfiler {
  long _totalTicks;
  long _maxTicks;
  int _countedFrames;
  int _hits;
  readonly Stopwatch _stopwatch = new();

  /// <summary>
  /// Starts timer and counts a new hit. The "hit" is usually a logic beginning of the branch being measured.
  /// </summary>
  public void StartNewHit() {
    _stopwatch.Start();
    _hits++;
  }

  /// <summary>Starts the stopped timer, but doesn't count it as a "hit".</summary>
  /// <remarks>Use it to resume a temporarily paused timer to not affect the hits stats.</remarks>
  public void Start() {
    _stopwatch.Start();
  }

  /// <summary>Pauses the timer.</summary>
  public void Stop() {
    _stopwatch.Stop();
  }

  /// <summary>Makes a snapshot of the stats gathered so far, and resets them to start a new measurement.</summary>
  public void NextFrame() {
    _stopwatch.Stop();
    var ticks = _stopwatch.ElapsedTicks;
    _maxTicks = Math.Max(_maxTicks, ticks);
    _totalTicks += ticks;
    _countedFrames++;
  }

  /// <summary>Returns stats string and resets all samples to start a new session.</summary>
  /// <returns></returns>
  public string GetStatsAndReset() {
    var result = new StringBuilder();
    var total = 1000f * _totalTicks / Stopwatch.Frequency;
    var max = 1000f * _maxTicks / Stopwatch.Frequency;
    var avg = total / _countedFrames;
    result.Append("Avg: ").Append(avg.ToString("0.00").PadLeft(7)).Append("ms; ");
    result.Append("Max: ").Append(max.ToString("0.00").PadLeft(7)).Append("ms; ");
    result.Append("Total: ").Append(total.ToString("0.00").PadLeft(8)).Append("ms; ");
    result.Append("Samples: ").Append(_countedFrames).Append("; ");
    result.Append("Hits: ").Append(_hits);
    _maxTicks = 0;
    _totalTicks = 0;
    _countedFrames = 0;
    _hits = 0;
    _stopwatch.Reset();
    return result.ToString();
  }
}

}

// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text;

namespace IgorZ.Automation.Utils {

/// <summary>Helper class to profile execution timings.</summary>
public sealed class CounterProfiler {
  int _counter;
  List<int> _values = new(20);
  readonly int _padWidth;

  /// <param name="padWidth">Numbers padding width to keep it formatted. Default: 7.</param>
  public CounterProfiler(int padWidth = 7) {
    _padWidth = padWidth;
  }

  /// <summary>Increments counter by the specified value. It can be negative or zero.</summary>
  public void Increment(int increment) {
    _counter += increment;
  }

  /// <summary>Makes a snapshot of the stats gathered so far, and resets them to start a new measurement.</summary>
  public void NextFrame() {
    _values.Add(_counter);
    _counter = 0;
  }

  /// <summary>Returns stats string and resets all samples to start a new session.</summary>
  /// <returns></returns>
  public string GetStatsAndReset() {
    var result = new StringBuilder();
    int min=int.MaxValue, max=0;
    var avg = 0f;
    _values.Sort();
    var valueCount = _values.Count;
    for (var index = 0; index < valueCount; index++) {
      var value = _values[index];
      min = Math.Min(min, value);
      max = Math.Max(max, value);
      avg += value;
    }
    avg /= valueCount;
    var mean = _values[valueCount / 2];

    result.Append("Avg: ").Append(avg.ToString("0.##").PadLeft(_padWidth)).Append("; ");
    result.Append("Mean: ").Append(mean.ToString().PadLeft(_padWidth)).Append("; ");
    result.Append("Min: ").Append(min.ToString().PadLeft(_padWidth)).Append("; ");
    result.Append("Max: ").Append(max.ToString().PadLeft(_padWidth)).Append("; ");
    result.Append("Samples: ").Append(valueCount);
    _values = new List<int>(20);
    return result.ToString();
  }
}

}

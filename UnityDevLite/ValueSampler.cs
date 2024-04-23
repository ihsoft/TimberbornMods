// Unity Development tools.
// Author: igor.zavoychinskiy@gmail.com
// This software is distributed under Public domain license.

using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberCommons {

/// <summary>Helper class to track a high frequency value change.</summary>
/// <remarks>
/// <p>
/// When it's not feasible to track each value due to the volume, use the sampler. It will give the aggregates of the
/// observed values. Those include: min, max, average, and mean values.
/// </p>
/// <p>
/// First, you need to determine how many sample values you need to keep. It depends on the anticipated frequency of the
/// samples. E.g. if you get 100 samples per second AND you want to make aggregates over a period of second, then you
/// need 100 as <c>samplingSize</c>. Then, you should start adding samples via <see cref="AddSample"/>! Get stats at any
/// time via <see cref="GetStats"/>.  
/// </p>
/// </remarks>
public sealed class ValueSampler {
  readonly Queue<double> _samples = new();
  readonly int _samplingSize;

  /// <summary>Creates a sampler of value for the given number of samples.</summary>
  /// <remarks>
  /// This sampler only tracks N last samples. Having more samples makes the stats more flat, but it also consumes CPU.
  /// And it can also hide the real problems in the perf values. This value must be appropriate for the task.
  /// </remarks>
  /// <param name="samplingSize">The maximum number of samples to keep at time.</param>
  public ValueSampler(int samplingSize) {
    _samplingSize = samplingSize;
  }

  /// <summary>Add a sample to count towards the stats.</summary>
  public void AddSample(double sample) {
    _samples.Enqueue(sample);
    while (_samples.Count > _samplingSize) {
      _samples.Dequeue();
    }
  }

  /// <summary>Erases all the collected samples. It brings the sampler to its initial state.</summary>
  public void ClearState() {
    _samples.Clear();
  }

  /// <summary>Gives the current status on the sampled value.</summary>
  /// <remarks>It returns <c>NaN</c> as values if no samples were added.</remarks>
  public (double min, double max, double avg, double mean) GetStats() {
    if (_samples.Count == 0) {
      return (double.NaN, double.NaN, double.NaN, double.NaN);
    }
    var samples = _samples.ToArray();
    var samplesNum = samples.Length;
    Array.Sort(samples);
    var min = double.MaxValue;
    var max = double.MinValue;
    var sum = 0.0;
    var mean = samples[samplesNum / 2];
    for (var i = 0; i < samplesNum; i++) {
      var sample = samples[i];
      sum += sample;
      min = min > sample ? sample : min;
      max = max < sample ? sample : max;
    }
    return (min, max, sum / samplesNum, mean);
  }
}

}

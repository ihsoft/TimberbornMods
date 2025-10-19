// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

/// <summary>Pauses the given stopwatch on construction and resumes it on disposal.</summary>
public sealed class PausedStopwatch : IDisposable {
  readonly Stopwatch _stopwatch;

  public PausedStopwatch(Stopwatch stopwatch) {
    _stopwatch = stopwatch;
    if (_stopwatch.IsRunning) {
      _stopwatch.Stop();
    }
  }

  public void Dispose() {
    if (!_stopwatch.IsRunning) {
      _stopwatch.Start();
    }
  }
}

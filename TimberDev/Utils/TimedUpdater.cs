// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

/// <summary>Helper class for managing high frequency updates.</summary>
public sealed class TimedUpdater {
  readonly float _updateThreshold;
  float _lastUpdate;

  /// <summary>Helper class for managing high frequency updates.</summary>
  /// <param name="updateThreshold">The minimum delay between the updates in seconds.</param>
  /// <param name="startNow">Tells whether the "last update" timestamp should be set to "now".</param>
  public TimedUpdater(float updateThreshold, bool startNow = false) {
    _updateThreshold = updateThreshold;
    if (startNow) {
      _lastUpdate = Time.unscaledTime;
    }
  }

  /// <summary>Updates the state of the object if the threshold is reached.</summary>
  /// <param name="updateAction">The action to call when the update is needed.</param>
  /// <param name="force">Indicates that the threshold should be ignored and the update must happen right away.</param>
  public void Update(Action updateAction, bool force = false) {
    if (force || _lastUpdate + _updateThreshold <= Time.unscaledTime) {
      _lastUpdate = Time.unscaledTime;
      updateAction();
    }
  }
}

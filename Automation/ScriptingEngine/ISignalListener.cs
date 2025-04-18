// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for classes that listen to signal changes.</summary>
/// <remarks>Listeners are only notified when the signal value actually changes.</remarks>
interface ISignalListener {
  /// <summary>The behavior that owns this listener.</summary>
  AutomationBehavior Behavior { get; }

  /// <summary>Called when the signal value changes.</summary>
  /// <param name="signalName">The signal, which value has changed.</param>
  void OnValueChanged(string signalName);
}

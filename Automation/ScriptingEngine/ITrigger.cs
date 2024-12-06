// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for event sources. Listeners can register to get the events.</summary>
/// <seealso cref="ITriggerEventListener"/>
interface ITrigger : IScriptableType {
  /// <summary>Registers listener for trigger events.</summary>
  void RegisterListener(ITriggerEventListener listener);

  /// <summary>Unregisters listener for trigger events.</summary>
  void UnregisterListener(ITriggerEventListener listener);
}
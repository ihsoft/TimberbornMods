// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for a trigger event sink. Listeners should register to get the events.</summary>
/// <ssaalso cref="ITrigger"/>
interface ITriggerEventListener {

  /// <summary>Trigger that the listener is registered to.</summary>
  ITrigger Trigger { get; }

  /// <summary>Name of the event that the listener is interested in.</summary>
  string Name { get; }

  /// <summary>Arguments of the event that the listener is interested in.</summary>
  /// <remarks>The number and types of the arguments depend on the event and are defined by the trigger.</remarks>
  IExpressionValue[] Args { get; }

  /// <summary>Called when the event is triggered.</summary>
  void OnEvent();

  /// <summary>Called when the trigger gets destroyed.</summary>
  void OnTriggerDestroyed();
}

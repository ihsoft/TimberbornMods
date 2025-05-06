// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for a component that can be used in the scripting engine.</summary>
interface IScriptable {
  /// <summary>The name of the scriptable component.</summary>
  public string Name { get; }

  /// <summary>Return the names of signals that the specified building provides.</summary>
  /// <remarks>It is an expensive call. Don't execute it in the tick handlers.</remarks>
  public string[] GetSignalNamesForBuilding(BaseComponent building);

  /// <summary>Returns a signal value source.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="building">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  /// <seealso cref="RegisterSignalChangeCallback"/>
  public Func<ScriptValue> GetSignalSource(string name, BaseComponent building);

  /// <summary>Returns the definition of the signal with the specified name.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="building">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public SignalDef GetSignalDefinition(string name, BaseComponent building);

  /// <summary>Returns a property value source.</summary>
  /// <remarks>
  /// It is a very basic value accessor. It is similar to a signal, but there are no callbacks and definitions.
  /// Primarily used by the "GetProperty" operators.
  /// </remarks>
  public Func<object> GetPropertySource(string name, BaseComponent building);

  /// <summary>Returns the names of actions that can be executed on the specified building.</summary>
  /// <remarks>It is an expensive call. Don't execute it in the tick handlers.</remarks>
  public string[] GetActionNamesForBuilding(BaseComponent building);

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building);

  /// <summary>Returns the definition of the action with the specified name.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public ActionDef GetActionDefinition(string name, BaseComponent building);

  /// <summary>Registers a callback called when the signal value changes.</summary>
  /// <param name="signalOperator">The signal to register.</param>
  /// <param name="host">The signal changes handler to be registered.</param>
  public void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host);

  /// <summary>Unregisters a signal value change callback.</summary>
  /// <param name="signalOperator">The signal to unregister.</param>
  /// <param name="host">The signal changes handler to be unregistered.</param>
  public void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host);

  /// <summary>Installs the necessary components for the action to properly work on the specified building.</summary>
  /// <param name="actionOperator">The action to install.</param>
  /// <param name="building">The component on which the signal is registered.</param>
  public void InstallAction(ActionOperator actionOperator, BaseComponent building);

  /// <summary>Uninstalls the components installed for the action to work on the specified building.</summary>
  /// <param name="actionOperator">The action to uninstall.</param>
  /// <param name="building">The component on which the signal is registered.</param>
  public void UninstallAction(ActionOperator actionOperator, BaseComponent building);
}

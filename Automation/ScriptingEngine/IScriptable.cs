// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Parser;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for a component that can be used in the scripting engine.</summary>
interface IScriptable {
  /// <summary>The name of the scriptable component.</summary>
  public string Name { get; }

  /// <summary>Return the names of signals that the specified building provides.</summary>
  /// <remarks>It is an expensive call. Don't execute it in the tick handlers.</remarks>
  public string[] GetSignalNamesForBuilding(AutomationBehavior behavior);

  /// <summary>Returns a signal value source.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="behavior">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  /// <seealso cref="RegisterSignalChangeCallback"/>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior);

  /// <summary>Returns the definition of the signal with the specified name.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="behavior">The component on which the signal is to be handled.</param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public SignalDef GetSignalDefinition(string name, AutomationBehavior behavior);

  /// <summary>Returns a property value source.</summary>
  /// <remarks>
  /// It is a very basic value accessor. It is similar to a signal, but there are no callbacks and definitions.
  /// The main purpose is to override the stock component properties with some derived ones. For example, if the stock
  /// component has a property of incompatible type, or there is no simple property that the rule preprocessor needs.
  /// </remarks>
  /// <param name="name">The full name of the property, including the scriptable component name.</param>
  /// <param name="behavior">The component which the property should be resolved on.</param>
  /// <returns>
  /// <p>
  /// A getter function that returns the property value as a native C# type. If the property value is NULL, then it
  /// should be translated to a non-NULL value based on its type and semantics: null-strings become "NULL", numbers
  /// become 0, etc.
  /// </p>
  /// <p>
  /// If the property is not supported, then the method result should be <c>NULL</c>, and the caller should decide how
  /// to react on it.
  /// </p>
  /// </returns>
  public Func<object> GetPropertySource(string name, AutomationBehavior behavior);

  /// <summary>Returns the names of actions that can be executed on the specified building.</summary>
  /// <remarks>It is an expensive call. Don't execute it in the tick handlers.</remarks>
  public string[] GetActionNamesForBuilding(AutomationBehavior behavior);

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="behavior">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior);

  /// <summary>Returns the definition of the action with the specified name.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="behavior">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public ActionDef GetActionDefinition(string name, AutomationBehavior behavior);

  /// <summary>Registers a callback called when the signal value changes.</summary>
  /// <param name="signalOperator">The signal to register.</param>
  /// <param name="host">The signal changes handler to be registered.</param>
  /// <exception cref="InvalidOperationException">if the signal is not found.</exception>
  public void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host);

  /// <summary>Unregisters a signal value change callback.</summary>
  /// <param name="signalOperator">The signal to unregister.</param>
  /// <param name="host">The signal changes handler to be unregistered.</param>
  /// <exception cref="InvalidOperationException">if the signal is not found.</exception>
  public void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host);

  /// <summary>Installs the necessary components for the action to properly work on the specified building.</summary>
  /// <remarks>The default behavior is NOOP. The action names aren't validated.</remarks>
  /// <param name="actionOperator">The action to install.</param>
  /// <param name="behavior">The component on which the action is to be registered.</param>
  public void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior);

  /// <summary>Uninstalls the components installed for the action to work on the specified building.</summary>
  /// <remarks>The default behavior is NOOP. The action names aren't validated.</remarks>
  /// <param name="actionOperator">The action to uninstall.</param>
  /// <param name="behavior">The component on which the action is to be unregistered.</param>
  public void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior);
}

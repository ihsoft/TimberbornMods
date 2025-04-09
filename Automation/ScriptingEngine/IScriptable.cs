// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Interface for a component that can be used in the scripting engine.</summary>
public interface IScriptable {
  /// <summary>The name of the scriptable component.</summary>
  public string Name { get; }

  /// <summary>Return the names of signals that the specified building provides.</summary>
  /// <remarks>It is an expensive call. Don't execute it in the tick handlers.</remarks>
  public string[] GetSignalNamesForBuilding(BaseComponent building);

  /// <summary>Returns a signal source that can be used to monitor the specified signal value.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="building">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public Func<ScriptValue> GetSignalSource(string name, BaseComponent building);

  /// <summary>Returns the definition of the signal with the specified name.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="building">The component on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public SignalDef GetSignalDefinition(string name, BaseComponent building);

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
  /// <param name="name">The name of the signal.</param>
  /// <param name="building">The component on which the signal is to be registered.</param>
  /// <param name="onValueChanged">The callback that is called when the signal value changes.</param>
  public void RegisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged);

  /// <summary>Unregisters a signal value change callback.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="building">The component on which the signal is registered.</param>
  /// <param name="onValueChanged">The callback that was registered for updates.</param>
  public void UnregisterSignalChangeCallback(string name, BaseComponent building, Action onValueChanged);

  /// <summary>Installs the necessary components for the action to properly work on the specified building.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The component on which the signal is registered.</param>
  public void InstallAction(string name, BaseComponent building);

  /// <summary>Uninstalls the components installed for the action to work on the specified building.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The component on which the signal is registered.</param>
  public void UninstallAction(string name, BaseComponent building);
}

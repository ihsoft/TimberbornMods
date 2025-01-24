// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Service that provides access to the scripting engine.</summary>
public sealed class ScriptingService : ILoadableSingleton {

  #region API

  /// <summary>Instance of the service for accessing from a static context.</summary>
  public static ScriptingService Instance;

  /// <summary>Registers a new scriptable component.</summary>
  public void RegisterScriptable(IScriptable scriptable) {
    _registedScriptables.Add(scriptable.Name, scriptable);
  }

  /// <summary>Returns a signal source by its name.</summary>
  /// <remarks>
  /// Different buildings have different signals. If requested for a wrong building, an error will be thrown.
  /// </remarks>
  /// <param name="name">The full dotted name of the signal. For example, "Weather.Season".</param>
  /// <param name="building">
  /// The building to get the signal for. Ignored for the global signals (like "Weather").
  /// </param>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public Func<ScriptValue> GetSignalSource(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetSignalSource(name, building);
  }

  /// <summary>Returns a signal definition by its name.</summary>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public SignalDef GetSignalDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetSignalDefinition(name, building);
  }

  /// <summary>Registers a callback called when the signal value changes.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="onValueChanged">The callback that is called when the signal value changes.</param>
  public void RegisterSignalChangeCallback(string name, Action onValueChanged) {
    if (!_signalChangeCallbacks.TryGetValue(name, out var callbacks)) {
      callbacks = [];
      _signalChangeCallbacks[name] = callbacks;
    }
    callbacks.Add(onValueChanged);
  }

  /// <summary>Unregisters a signal value change callback.</summary>
  /// <param name="name">The name of the signal.</param>
  /// <param name="onValueChanged">The callback that was registered for updates.</param>
  public void UnregisterSignalChangeCallback(string name, Action onValueChanged) {
    if (_signalChangeCallbacks.TryGetValue(name, out var callbacks)) {
      callbacks.Remove(onValueChanged);
    }
  }

  /// <summary>Notifies all registered callbacks about a signal change.</summary>
  /// <param name="name"></param>
  public void NotifySignalChanged(string name) {
    if (!_signalChangeCallbacks.TryGetValue(name, out var callbacks)) {
      return;
    }
    foreach (var callback in callbacks) {
      callback();
    }
  }

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The building on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetActionExecutor(name, building);
  }

  /// <summary>Returns the definition of the action by its name.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The building on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public ActionDef GetActionDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetActionDefinition(name, building);
  }

  /// <summary>Returns all signal names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetSignalNamesForBuilding(BaseComponent building) {
    return _registedScriptables.Values
        .SelectMany(s => s.GetSignalNamesForBuilding(building))
        .ToArray();
  }

  /// <summary>Returns all action names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetActionNamesForBuilding(BaseComponent building) {
    return _registedScriptables.Values
        .SelectMany(s => s.GetActionNamesForBuilding(building))
        .ToArray();
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {}

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registedScriptables = [];
  readonly Dictionary<string, List<Action>> _signalChangeCallbacks = new();

  ScriptingService() {
    Instance = this;
  }

  #endregion
}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
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
    _registeredScriptables.Add(scriptable.Name, scriptable);
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
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetSignalSource(name, building);
  }

  /// <summary>Returns a signal definition by its name.</summary>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public SignalDef GetSignalDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetSignalDefinition(name, building);
  }

  /// <inheritdoc cref="IScriptable.RegisterSignalChangeCallback"/>
  public void RegisterSignalChangeCallback(string name, AutomationBehavior building, Action onValueChanged) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    scriptable.RegisterSignalChangeCallback(name, building, onValueChanged);
  }

  /// <inheritdoc cref="IScriptable.UnregisterSignalChangeCallback"/>
  public void UnregisterSignalChangeCallback(string name, AutomationBehavior building, Action onValueChanged) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    scriptable.UnregisterSignalChangeCallback(name, building, onValueChanged);
  }

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">The building on which the action is to be executed.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
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
    if (!_registeredScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + nameItems[0]);
    }
    return scriptable.GetActionDefinition(name, building);
  }

  /// <summary>Returns all signal names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetSignalNamesForBuilding(BaseComponent building) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetSignalNamesForBuilding(building))
        .ToArray();
  }

  /// <summary>Returns all action names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetActionNamesForBuilding(BaseComponent building) {
    return _registeredScriptables.Values
        .SelectMany(s => s.GetActionNamesForBuilding(building))
        .ToArray();
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {}

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registeredScriptables = [];

  ScriptingService() {
    Instance = this;
  }

  #endregion
}

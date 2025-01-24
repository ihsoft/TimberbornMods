// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    var (scriptable, instance) = GetScriptable(nameItems[0], building);
    return scriptable.GetSignalSource(name, instance);
  }

  /// <summary>Returns a signal definition by its name.</summary>
  /// <exception cref="ScriptError">if the signal is not found.</exception>
  public SignalDef GetSignalDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    var (scriptable, instance) = GetScriptable(nameItems[0], building);
    return scriptable.GetSignalDefinition(name, instance);
  }

  /// <summary>Registers a callback that is called when the signal value changes.</summary>
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
  /// <param name="building">
  /// The building on which the action is to be executed. It must have the component that the action is bound to.
  /// See <see cref="IScriptable.InstanceType"/>.
  /// </param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    var (scriptable, instance) = GetScriptable(nameItems[0], building);
    return scriptable.GetActionExecutor(name, instance);
  }

  /// <summary>Returns the definition of the action by its name.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">
  /// The building on which the action is to be executed. It must have the component that the action is bound to.
  /// See <see cref="IScriptable.InstanceType"/>.
  /// </param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public ActionDef GetActionDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    var (scriptable, instance) = GetScriptable(nameItems[0], building);
    return scriptable.GetActionDefinition(name, instance);
  }

  /// <summary>Returns all signal names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetSignalNamesForBuilding(BaseComponent building) {
    return _registedScriptables.Values
        .Where(s => s.InstanceType == null || TryGetComponentFast(building, s.InstanceType, out _))
        .SelectMany(s => s.GetSignalNamesForBuilding(building))
        .ToArray();
  }

  /// <summary>Returns all action names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetActionNamesForBuilding(BaseComponent building) {
    return _registedScriptables.Values
        .Where(s => s.InstanceType == null || TryGetComponentFast(building, s.InstanceType, out _))
        .SelectMany(s => s.GetActionNamesForBuilding(building))
        .ToArray();
  }

  /// <summary>Returns a component of the specified type from the building.</summary>
  /// <remarks>It is a counter-part to the <see cref="BaseComponent.GetComponentFast{T}"/>.</remarks>
  public static BaseComponent GetComponentFast(BaseComponent building, Type type) {
    var genericMethodInfo = _getComponentFastMethod.MakeGenericMethod(type);
    var component = genericMethodInfo.Invoke(building, []) as BaseComponent;
    if (!component) {
      throw new ScriptError($"The building doesn't have component: " + type);
    }
    return component;
  }

  /// <summary>Returns a component of the specified type from the building.</summary>
  public static bool TryGetComponentFast(BaseComponent building, Type type, out BaseComponent component) {
    var genericMethodInfo = _getComponentFastMethod.MakeGenericMethod(type);
    component = genericMethodInfo.Invoke(building, []) as BaseComponent;
    return component;
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {}

  #endregion

  #region Implementation

  readonly Dictionary<string, IScriptable> _registedScriptables = [];
  readonly Dictionary<string, List<Action>> _signalChangeCallbacks = new();

  static MethodInfo _getComponentFastMethod = typeof(BaseComponent).GetMethod(
      nameof(BaseComponent.GetComponentFast), BindingFlags.Instance | BindingFlags.Public);

  ScriptingService() {
    Instance = this;
    _getComponentFastMethod = typeof(BaseComponent).GetMethod(
        nameof(BaseComponent.GetComponentFast), BindingFlags.Instance | BindingFlags.Public);
    if (_getComponentFastMethod == null) {
      throw new ScriptError("Cannot find GetComponentFast method in BaseComponent");
    }
  }

  (IScriptable, BaseComponent) GetScriptable(string name, BaseComponent building) {
    if (!_registedScriptables.TryGetValue(name, out var scriptable)) {
      throw new ScriptError("Unknown scriptable component: " + name);
    }
    if (scriptable.InstanceType != null) {
      building = GetComponentFast(building, scriptable.InstanceType);
      if (!building) {
        throw new ScriptError("The building doesn't have component: " + scriptable.InstanceType.FullName);
      }
    }
    return (scriptable, building);
  }

  #endregion
}
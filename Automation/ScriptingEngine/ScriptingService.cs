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

sealed class ScriptingService : ILoadableSingleton {

  #region API

  /// <summary>Instance of the service for accessing from a static context.</summary>
  public static ScriptingService Instance;

  /// <summary>Registers a new scriptable component.</summary>
  public void RegisterScriptable(IScriptable scriptable) {
    _registedScriptables.Add(scriptable.Name, scriptable);
  }

  /// <summary>Returns a trigger source by its name.</summary>
  /// <remarks>
  /// Different buildings have different triggers. If requested for a wrong building, an error will be thrown.
  /// </remarks>
  /// <param name="name">The full dotted name of the trigger. For example, "Weather.Season".</param>
  /// <param name="building">
  /// The building to get the trigger for. Ignored for the global triggers (like "Weather").
  /// </param>
  /// <param name="onValueChanged">The callback to call when the trigger value changes.</param>
  /// <exception cref="ScriptError">if the trigger is not found.</exception>
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged) {
    var nameItems = name.Split('.');
    var (scriptable, instance) = GetScriptable(nameItems[0], building);
    return scriptable.GetTriggerSource(nameItems[1], instance, onValueChanged);
  }

  /// <summary>Returns a trigger definition by its name.</summary>
  /// <exception cref="ScriptError">if the trigger is not found.</exception>
  public TriggerDef GetTriggerDefinition(string name, BaseComponent building) {
    var nameItems = name.Split('.');
    var (scriptable, instance) = GetScriptable(nameItems[0], building);
    return scriptable.GetTriggerDefinition(nameItems[1], instance);
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
    return scriptable.GetActionExecutor(nameItems[1], instance);
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
    return scriptable.GetActionDefinition(nameItems[1], instance);
  }

  /// <summary>Returns all trigger names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetTriggersForBuilding(BaseComponent building) {
    return _registedScriptables.Values
        .Where(s => s.InstanceType == null || TryGetComponentFast(building, s.InstanceType, out _))
        .SelectMany(s => s.GetTriggerNamesForBuilding(building))
        .ToArray();
  }

  /// <summary>Returns all action names for the specified building.</summary>
  /// <remarks>This can be an expensive call. Avoid making it in the ticks.</remarks>
  public string[] GetActionForBuilding(BaseComponent building) {
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
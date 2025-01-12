// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
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
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown trigger: " + name);
    }
    if (scriptable.InstanceType != null) {
      building = GetComponentFast(building, scriptable.InstanceType);
    }
    return scriptable.GetTriggerSource(nameItems[1], building, onValueChanged);
  }

  /// <summary>Returns a trigger definition by its name.</summary>
  /// <exception cref="ScriptError">if the trigger is not found.</exception>
  public IScriptable.TriggerDef GetTriggerDefinition(string name) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown trigger: " + name);
    }
    return scriptable.GetTriggerDefinition(nameItems[1]);
  }

  /// <summary>Returns an executor that executes the specified action with the provided arguments.</summary>
  /// <param name="name">The name of the action.</param>
  /// <param name="building">
  /// The building on which the action is to be executed. It must have the component that the action is bound to.
  /// See <see cref="IScriptable.InstanceType"/>.
  /// </param>
  /// <param name="args">The arguments for the action. The number, type, and meaning depend on the action.</param>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public Action GetActionExecutor(string name, BaseComponent building, string[] args) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown action: " + name);
    }
    if (scriptable.InstanceType != null) {
      building = GetComponentFast(building, scriptable.InstanceType);
    }
    return scriptable.GetActionExecutor(nameItems[1], building, args);
  }

  /// <summary>Returns the definition of the action by its name.</summary>
  /// <exception cref="ScriptError">if action is not found.</exception>
  public IScriptable.ActionDef GetActionDefinition(string name) {
    var nameItems = name.Split('.');
    if (!_registedScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      throw new ScriptError("Unknown action: " + name);
    }
    return scriptable.GetActionDefinition(nameItems[1]);
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

  static BaseComponent GetComponentFast(BaseComponent building, Type type) {
    var genericMethodInfo = _getComponentFastMethod.MakeGenericMethod(type);
    var component = genericMethodInfo.Invoke(building, []) as BaseComponent;
    if (!component) {
      throw new ScriptError($"The building doesn't have component: " + type);
    }
    return component;
  }

  #endregion
}
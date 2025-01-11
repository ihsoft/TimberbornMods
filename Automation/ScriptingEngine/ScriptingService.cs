// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using Timberborn.BaseComponentSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.Automation.ScriptingEngine;

sealed class ScriptingService : ILoadableSingleton {

  #region API

  /// <summary>Instance of the service for accessing from a static context.</summary>
  public static ScriptingService Instance;

  /// <summary>Returns a trigger source by its name.</summary>
  /// <remarks>
  /// Different buildings have different triggers. If requested for a wrong building, an error will be thrown.
  /// </remarks>
  /// <param name="name">The full dotted name of the trigger. For example, "Weather.Season".</param>
  /// <param name="building">
  /// The building to get the trigger for. Ignored for the global triggers (like "Weather").
  /// </param>
  /// <param name="onValueChanged">The callback to call when the trigger value changes.</param>
  /// <exception cref="ScriptError">if the trigger is not found for the building.</exception>
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged) {
    var nameItems = name.Split('.');
    if (_globalScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      return scriptable.GetTriggerSource(nameItems[1], building, onValueChanged);
    }
    throw new ScriptError("Unknown trigger: " + name);
  }

  public IScriptable.TriggerDef GetTriggerDefinition(string name) {
    var nameItems = name.Split('.');
    if (_globalScriptables.TryGetValue(nameItems[0], out var scriptable)) {
      return scriptable.GetTriggerDefinition(nameItems[1]);
    }
    throw new ScriptError("Unknown trigger: " + name);
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {}

  #endregion

  #region Implementation

  readonly Dictionary<string,IScriptable> _globalScriptables = new(); 

  ScriptingService(WeatherScriptableComponent weatherScriptableComponent) {
    Instance = this;
    _globalScriptables.Add(weatherScriptableComponent.Name, weatherScriptableComponent);
  }

  #endregion
}
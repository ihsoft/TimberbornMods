// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Values;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : BaseComponent, ITrigger, IScriptableInstance,
                                            IDeletableEntity, IPostInitializableLoadedEntity {

  /// <inheritdoc/>
  public string ScriptableTypeName => "Floodgate";

  #region AbstractTrigger implementation

  readonly HashSet<ITriggerEventListener> _heightChangeListeners = [];

  /// <inheritdoc/>
  public void RegisterListener(ITriggerEventListener listener) {
    switch (listener.Name) {
      case "HeightChangedEvent":
        if (listener.Args.Length != 0) {
          throw new ScriptError("HeightChangedEvent takes no arguments");
        }
        if (!_heightChangeListeners.Add(listener)) {
          throw new ScriptError("Already registered for HeightChangedEvent");
        }
        break;
      default:
        throw new Exception("Unknown event name: " + name);
    }
  }

  /// <inheritdoc/>
  public void UnregisterListener(ITriggerEventListener listener) {
    _heightChangeListeners.Remove(listener);
  }

  #endregion

  #region IPostInitializableLoadedEntity implementation

  /// <inheritdoc/>
  void IPostInitializableLoadedEntity.PostInitializeLoadedEntity() {
    _lastHeight = Mathf.RoundToInt(_floodgate.Height * 100);
  }

  #endregion

  #region IDeletableEntity implementation

  /// <inheritdoc/>
  void IDeletableEntity.DeleteEntity() {
    foreach (var listener in _heightChangeListeners) {
      listener.OnTriggerDestroyed();
    }
  }

  #endregion

  #region Script methods

  [IScriptableInstance.ScriptFunction]
  public NumberValue GetHeight() => NumberValue.FromRawValue(_lastHeight);

  [IScriptableInstance.ScriptFunction]
  public void SetHeight(int height) {
    if (height < 0) {
      height = _floodgate.MaxHeight - height;
    }
    _floodgate.SetHeight(height / 100f);
  }

  [IScriptableInstance.ScriptFunction]
  public void SetMaxHeight() {
    _floodgate.SetHeight(_floodgate.MaxHeight);
  }

  #endregion

  Floodgate _floodgate;
  int _lastHeight = -1;  // 2-digits fixed point float.

  void Awake() {
    _floodgate = GetComponentFast<Floodgate>();
  }

  /// <summary>Called from the patch to notify about the height change.</summary>
  internal void OnSetHeight(float height) {
    var newHeight = Mathf.RoundToInt(height * 100);
    if (_lastHeight == newHeight) {
      return;
    }
    _lastHeight = newHeight;
    foreach (var listener in _heightChangeListeners) {
      listener.OnEvent();
    }
  }
}
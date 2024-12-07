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
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>Scriptable component for the Floodgate building.</summary>
/// <remarks>It contains both the triggers and the scripting methods.</remarks>
sealed class FloodgateScriptableComponent : BaseComponent, ITrigger, IScriptableInstance,
                                            IDeletableEntity, IPostInitializableLoadedEntity {

  /// <inheritdoc/>
  public string ScriptableTypeName => ComponentsIndex.TypeToName[typeof(FloodgateScriptableComponent)];

  #region AbstractTrigger implementation

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

  #region Methods available to the scripts
  // ReSharper disable UnusedMember.Global

  [IScriptableInstance.ScriptFunction]
  public NumberValue GetHeight() => NumberValue.FromRawValue(_lastHeight);

  [IScriptableInstance.ScriptFunction]
  public void SetHeight(IExpressionValue varHeight) {
    var height = varHeight.AsNumber();
    if (height < 0) {
      height = _floodgate.MaxHeight * 100 + height;
    }
    _floodgate.SetHeight(height / 100f);
  }

  [IScriptableInstance.ScriptFunction]
  public void SetMaxHeight() {
    _floodgate.SetHeight(_floodgate.MaxHeight);
  }

  // ReSharper restore UnusedMember.Global
  #endregion

  #region Implementation

  Floodgate _floodgate;
  int _lastHeight = -1;  // 2-digits fixed point float.
  bool _isRunning;

  readonly HashSet<ITriggerEventListener> _heightChangeListeners = [];

  void Awake() {
    _floodgate = GetComponentFast<Floodgate>();
  }

  /// <summary>Called from the patch to notify about the height change.</summary>
  /// <seealso cref="FloodgateSetHeightPatch"/>
  internal void OnSetHeight(float height) {
    var newHeight = Mathf.RoundToInt(height * 100);
    if (_lastHeight == newHeight) {
      return;
    }
    _lastHeight = newHeight;

    // Prevent cyclic calls when the height being changed on the Self object.
    if (_isRunning) {
      _isRunning = false;
      HostedDebugLog.Warning(this, "Height change on Self is detected. Skipping the event.");
      return;
    }

    _isRunning = true;
    foreach (var listener in _heightChangeListeners) {
      listener.OnEvent();
    }
    _isRunning = false;
  }

  #endregion
}

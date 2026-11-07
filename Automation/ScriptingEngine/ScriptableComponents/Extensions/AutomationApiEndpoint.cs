// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Versioned reflection entrypoint for the independently compiled source helper.</summary>
public sealed class AutomationApiEndpoint : ILoadableSingleton, IUnloadableSingleton {
  /// <summary>Returns standard-library delegates for this game container, or null when it is not active.</summary>
  /// <remarks>
  /// The returned dictionary contains "register" (Action of a declaration dictionary), "update" (Action of provider ID,
  /// signal ID and BaseComponent), and "active" (Func of bool). All calls belong to this container's game thread.
  /// The active delegate becomes false on unload; callers must create a new helper for another game context.
  /// No provider-owned code or signal evaluation runs during connection. Keep this V1 contract stable.
  /// </remarks>
  public static IDictionary<string, Delegate> ConnectV1(IContainer container) {
    var endpoint = _current;
    if (endpoint == null || !endpoint._active || !ReferenceEquals(endpoint._container, container)) {
      return null;
    }
    return new Dictionary<string, Delegate> {
        ["register"] = new Action<IDictionary<string, object>>(endpoint._component.Register),
        ["update"] = new Action<string, string, BaseComponent>(endpoint._component.UpdateSignal),
        ["active"] = new Func<bool>(() => endpoint._active),
    };
  }

  /// <inheritdoc/>
  public void Load() {
    _active = true;
    _current = this;
  }

  /// <inheritdoc/>
  public void Unload() {
    _active = false;
    if (ReferenceEquals(_current, this)) {
      _current = null;
    }
  }

  static AutomationApiEndpoint _current;
  readonly IContainer _container;
  readonly ExternalScriptableComponent _component;
  bool _active;

  AutomationApiEndpoint(IContainer container, ExternalScriptableComponent component) {
    _container = container;
    _component = component;
  }
}

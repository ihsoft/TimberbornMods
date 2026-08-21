// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.Automation;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

/// <summary>Tracks listeners of stock automation signals referenced by stable entity ID.</summary>
sealed class AutomatorSignalReferenceManager : ILoadableSingleton, IUnloadableSingleton {

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {
    _eventBus.Register(this);
  }

  #endregion

  #region IUnloadableSingleton implementation

  /// <inheritdoc/>
  public void Unload() {
    _eventBus.Unregister(this);
    _registrants.Clear();
    if (ReferenceEquals(Instance, this)) {
      Instance = null;
    }
  }

  #endregion

  #region API

  /// <summary>The active game-context instance used by the Automator Harmony bridge.</summary>
  public static AutomatorSignalReferenceManager Instance { get; private set; }

  /// <summary>Registers one signal-operator/listener pair for a referenced transmitter.</summary>
  public void Register(Guid entityId, SignalOperator signalOperator, ISignalListener listener) {
    if (!_registrants.TryGetValue(entityId, out var listeners)) {
      listeners = [];
      _registrants.Add(entityId, listeners);
    }
    if (!listeners.TryGetValue(listener, out var signalOperators)) {
      signalOperators = [];
      listeners.Add(listener, signalOperators);
    }
    if (!signalOperators.Add(signalOperator)) {
      throw new InvalidOperationException(
          $"Automator signal listener already registered: {entityId}/{listener}/{signalOperator}");
    }
  }

  /// <summary>Unregisters one signal-operator/listener pair for a referenced transmitter.</summary>
  public void Unregister(Guid entityId, SignalOperator signalOperator, ISignalListener listener) {
    if (!_registrants.TryGetValue(entityId, out var listeners)
        || !listeners.TryGetValue(listener, out var signalOperators)
        || !signalOperators.Remove(signalOperator)) {
      throw new InvalidOperationException(
          $"Automator signal listener not registered: {entityId}/{listener}/{signalOperator}");
    }
    if (signalOperators.Count == 0) {
      listeners.Remove(listener);
    }
    if (listeners.Count == 0) {
      _registrants.Remove(entityId);
    }
  }

  /// <summary>Notifies listeners after a stock Automator's observable state changes.</summary>
  public void OnStateChanged(Automator automator) {
    Notify(automator.GetComponent<EntityComponent>().EntityId);
  }

  #endregion

  #region Event handlers

  /// <summary>Notifies references when a stock transmitter becomes available.</summary>
  [OnEvent]
  public void OnEntityInitialized(EntityInitializedEvent @event) {
    var automator = @event.Entity.GetComponent<Automator>();
    if (automator?.IsTransmitter == true) {
      Notify(@event.Entity.EntityId);
    }
  }

  /// <summary>Notifies references when their stock transmitter is deleted.</summary>
  [OnEvent]
  public void OnEntityDeleted(EntityDeletedEvent @event) {
    var automator = @event.Entity.GetComponent<Automator>();
    if (automator?.IsTransmitter == true) {
      Notify(@event.Entity.EntityId);
    }
  }

  /// <summary>Refreshes unresolved references after the automation service finishes loading.</summary>
  [OnEvent]
  public void OnAutomationSystemReady(AutomationServiceReadyEvent @event) {
    foreach (var entityId in _registrants.Keys.ToArray()) {
      if (!_automatorRegistry.FindTransmitterById(entityId)) {
        Notify(entityId);
      }
    }
  }

  #endregion

  #region Implementation

  readonly AutomatorRegistry _automatorRegistry;
  readonly EventBus _eventBus;
  readonly Dictionary<Guid, Dictionary<ISignalListener, HashSet<SignalOperator>>> _registrants = [];

  AutomatorSignalReferenceManager(AutomatorRegistry automatorRegistry, EventBus eventBus) {
    _automatorRegistry = automatorRegistry;
    _eventBus = eventBus;
    Instance = this;
  }

  void Notify(Guid entityId) {
    if (!_registrants.TryGetValue(entityId, out var listeners)) {
      return;
    }
    var registrations = listeners.Select(pair => (SignalName: pair.Value.First().SignalName, Listener: pair.Key))
        .ToArray();  // Need a copy! The listeners dictionary can be modified in the loop.
    foreach (var registration in registrations) {
      ScriptingService.Instance.NotifySignalListener(registration.SignalName, registration.Listener);
    }
  }

  #endregion
}

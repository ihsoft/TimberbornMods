// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Timberborn.TickSystem;
using UnityEngine;

namespace IgorZ.Automation.TickerSystem;

/// <summary>Simple service that ticks all listeners.</summary>
/// <remarks>
/// <p>
/// Use it for registering dynamic objects. The stock game <c>TickableComponent</c> registers once and can't be
/// unregistered until the Mono object died.
/// </p>
/// <p>
/// Listeners are called in the undefined order. They must register via <see cref="AddListener"/> and deregister via
/// <see cref="RemoveListener"/>. For the MonoBehaviour objects, it is allowed to not deregister, but it is recommended
/// to do so for the well-defined life cycle. 
/// </p>
/// </remarks>
public class TickerService : ITickableSingleton {

  readonly List<ITicksListener> _listeners = new();

  #region API

  /// <summary>Interface for the objects that want to be notified about the ticks.</summary>
  public interface ITicksListener {
    /// <summary>Tick the listener.</summary>
    void TickerServiceTick();
  }

  /// <summary>Registers a listener.</summary>
  public void AddListener(ITicksListener listener) {
    _listeners.Add(listener);
  }

  /// <summary>De-registers a listener.</summary>
  /// <remarks>
  /// The MonoBehaviour objects that are being destroyed can skip de-registering. They will be removed automatically on
  /// the next tick.
  /// </remarks>
  public void RemoveListener(ITicksListener listener) {
    _listeners.Remove(listener);
  }

  #endregion

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    for (var i = _listeners.Count - 1; i >= 0; i--) {
      var listener = _listeners[i];
      if (listener == null || listener is MonoBehaviour behaviour && !behaviour) {
        _listeners.RemoveAt(i);
        continue;
      }
      listener.TickerServiceTick();
    }
  }

  #endregion

}
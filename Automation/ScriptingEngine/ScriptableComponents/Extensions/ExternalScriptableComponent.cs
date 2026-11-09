// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.BaseComponentSystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Adapts configured capabilities to native script definitions and deferred signal observations.</summary>
sealed class ExternalScriptableComponent : ScriptableComponentBase, IUnloadableSingleton, ISaveableSingleton {
  /// <summary>Building-owned persistence and subscriptions for configured signals.</summary>
  public sealed class SignalTracker : AbstractStatusTracker;

  sealed class GlobalSignalTracker(ReferenceManager references)
      : AbstractGlobalStatusTracker(new SingletonKey("IgorZ.Automation.ConfiguredSignals"), references);

  sealed class Signal {
    public SignalDef Definition;
    public Func<BaseComponent, ScriptValue> Read;
    public Func<BaseComponent, BaseComponent> Resolve;
    public readonly Dictionary<object, Watch> Watches = new();
  }

  sealed class ExternalAction {
    public ActionDef Definition;
    public Action<BaseComponent, ScriptValue[]> Execute;
    public Func<BaseComponent, BaseComponent> Resolve;
  }

  sealed class Watch {
    public Signal Signal;
    public object Key;
    public BaseComponent Source;
    public SignalTracker Tracker;
    public ScriptValue Value;
    public ScriptError.RuntimeError Error;
  }

  readonly record struct PendingHook(Action<object> Dispatch, object Source) {
    /// <summary>Compares identities without invoking provider-defined equality on the setter's stack.</summary>
    public bool Equals(PendingHook other) {
      return ReferenceEquals(Dispatch, other.Dispatch) && ReferenceEquals(Source, other.Source);
    }

    /// <summary>Hashes identities without invoking provider-defined hashing.</summary>
    public override int GetHashCode() {
      return HashCode.Combine(
          RuntimeHelpers.GetHashCode(Dispatch), Source == null ? 0 : RuntimeHelpers.GetHashCode(Source));
    }
  }

  sealed class UpdateRunner : MonoBehaviour;

  /// <inheritdoc/>
  public override string Name => "Mods";

  /// <inheritdoc/>
  public override void Load() {
    base.Load();
    _globalTracker = new GlobalSignalTracker(new ReferenceManager(ScriptingService));
    _globalTracker.Load(_singletonLoader);
    _runner = new GameObject("Automation.ExternalSignals").AddComponent<UpdateRunner>();
    _active = true;
  }

  /// <inheritdoc/>
  public void Save(ISingletonSaver singletonSaver) => _globalTracker.Save(singletonSaver);

  /// <inheritdoc/>
  public void Unload() {
    _active = false;
    if (_runner) {
      UnityEngine.Object.Destroy(_runner.gameObject);
    }
    _runner = null;
    _processing = null;
    _pending.Clear();
    _pendingHooks.Clear();
    _signals.Clear();
    _actions.Clear();
    _globalTracker = null;
  }

  /// <summary>Registers a native signal and reader. A duplicate never replaces the first registration.</summary>
  public bool RegisterSignal(SignalDef definition, Func<BaseComponent, ScriptValue> read,
      Func<BaseComponent, BaseComponent> resolve) {
    if (IsRegistered("signal", definition.ScriptName)) {
      return false;
    }
    _signals.Add(definition.ScriptName, new Signal { Definition = definition, Read = read, Resolve = resolve });
    DebugEx.Info("Registered external automation signal: {0}", definition.ScriptName);
    return true;
  }

  /// <summary>Registers a native action and executor; all CLR argument conversion belongs to the executor.</summary>
  public void RegisterAction(ActionDef definition, Action<BaseComponent, ScriptValue[]> execute,
      Func<BaseComponent, BaseComponent> resolve) {
    if (IsRegistered("action", definition.ScriptName)) {
      return;
    }
    _actions.Add(definition.ScriptName, new ExternalAction {
        Definition = definition, Execute = execute, Resolve = resolve,
    });
    DebugEx.Info("Registered external automation action: {0}", definition.ScriptName);
  }

  /// <summary>Coalesces a hook/source pair for end-of-frame dispatch; no signal callbacks run here.</summary>
  /// <param name="dispatch">The hook's stable callback, allocated once when the patch is installed.</param>
  /// <param name="source">The setter instance, or null for a static setter; compared by reference identity.</param>
  public void ScheduleHook(Action<object> dispatch, object source) {
    if (!_active) {
      return;
    }
    _pendingHooks.Add(new PendingHook(dispatch, source));
    _processing ??= _runner.StartCoroutine(ProcessUpdates());
  }

  /// <summary>Only enqueues an existing observation; no provider delegates run in this call.</summary>
  public void UpdateSignal(string name, BaseComponent source) {
    if (!_active || !_signals.TryGetValue(name, out var signal)) {
      return;
    }
    if (signal.Resolve != null && !source || signal.Resolve == null && source) {
      return;
    }
    var key = signal.Resolve == null ? GlobalSource : (object)source.GameObject;
    if (!signal.Watches.TryGetValue(key, out var watch)) {
      return;
    }
    _pending.Add(watch);
    _processing ??= _runner.StartCoroutine(ProcessUpdates());
  }

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return _signals.Where(x => IsAvailable(x.Value.Resolve, behavior)).Select(x => x.Key).ToArray();
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return _actions.Where(x => IsAvailable(x.Value.Resolve, behavior)).Select(x => x.Key).ToArray();
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var signal = GetSignal(name);
    ResolveSource(signal.Resolve, behavior);
    return signal.Definition;
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var action = GetAction(name);
    ResolveSource(action.Resolve, behavior);
    return action.Definition;
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var signal = GetSignal(name);
    var source = ResolveSource(signal.Resolve, behavior);
    var key = signal.Resolve == null ? GlobalSource : (object)behavior.GameObject;
    return () => {
      if (signal.Watches.TryGetValue(key, out var watch)) {
        if (watch.Error != null) {
          throw watch.Error;
        }
        return watch.Value;
      }
      if (signal.Resolve == null && _globalTracker.TryGetSignalValue(name, out var globalValue)) {
        return globalValue;
      }
      if (signal.Resolve != null && behavior.TryGetDynamicComponent<SignalTracker>(out var tracker)
          && tracker.TryGetSignalValue(name, out var buildingValue)) {
        return buildingValue;
      }
      return Read(signal, source);
    };
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var action = GetAction(name);
    var source = ResolveSource(action.Resolve, behavior);
    return args => {
      AssertActionArgsCount(name, args, action.Definition.Arguments.Length);
      try {
        if (action.Resolve != null && !source) {
          throw new ScriptError.BadValue("External action source no longer exists.");
        }
        action.Execute(source, args);
      } catch (ScriptError.RuntimeError) {
        throw;
      } catch (Exception e) {
        DebugEx.Error("External action failed: {0}\n{1}", name, e);
        throw new ScriptError.InternalError("External action failed: " + name);
      }
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var signal = GetSignal(signalOperator.SignalName);
    var source = ResolveSource(signal.Resolve, host.Behavior);
    var key = signal.Resolve == null ? GlobalSource : (object)host.Behavior.GameObject;
    if (!signal.Watches.TryGetValue(key, out var watch)) {
      watch = new Watch {
          Signal = signal, Source = source, Key = key,
          Tracker = signal.Resolve != null ? host.Behavior.GetOrCreate<SignalTracker>() : null,
      };
      var restored = watch.Tracker != null
          ? watch.Tracker.TryGetSignalValue(signalOperator.SignalName, out watch.Value)
          : _globalTracker.TryGetSignalValue(signalOperator.SignalName, out watch.Value);
      if (!restored) {
        Refresh(watch);
      }
      if (watch.Tracker != null) {
        watch.Tracker.InitializeSignalValue(signalOperator.SignalName, watch.Value);
      } else {
        _globalTracker.SetSignalValue(signalOperator.SignalName, watch.Value);
      }
      signal.Watches.Add(key, watch);
    }
    if (watch.Tracker != null) {
      watch.Tracker.AddSignal(signalOperator, host);
    } else {
      _globalTracker.AddSignal(signalOperator, host);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var signal = GetSignal(signalOperator.SignalName);
    var key = signal.Resolve == null ? GlobalSource : (object)host.Behavior.GameObject;
    if (!signal.Watches.TryGetValue(key, out var watch)) {
      return;
    }
    var hasListeners = watch.Tracker != null
        ? watch.Tracker.RemoveSignal(signalOperator, host)
        : _globalTracker.RemoveSignal(signalOperator, host);
    if (!hasListeners) {
      signal.Watches.Remove(key);
      _pending.Remove(watch);
      if (watch.Tracker == null) {
        _globalTracker.ForgetSignalValue(signalOperator.SignalName);
      }
    }
  }

  static readonly object GlobalSource = new();
  readonly Dictionary<string, Signal> _signals = new();
  readonly Dictionary<string, ExternalAction> _actions = new();
  readonly HashSet<Watch> _pending = new();
  readonly HashSet<PendingHook> _pendingHooks = new();
  readonly ISingletonLoader _singletonLoader;
  GlobalSignalTracker _globalTracker;
  UpdateRunner _runner;
  Coroutine _processing;
  bool _active;

  ExternalScriptableComponent(ISingletonLoader singletonLoader) {
    _singletonLoader = singletonLoader;
  }

  bool IsRegistered(string kind, string name) {
    if (!(kind == "signal" && _signals.ContainsKey(name) || kind == "action" && _actions.ContainsKey(name))) {
      return false;
    }
    DebugEx.Error("Duplicate external automation {0}: {1}. Keeping the first registration; ignoring this one.",
        kind, name);
    return true;
  }

  Signal GetSignal(string name) {
    return _signals.TryGetValue(name, out var signal) ? signal : throw new UnknownSignalException(name);
  }

  ExternalAction GetAction(string name) {
    return _actions.TryGetValue(name, out var action) ? action : throw new UnknownActionException(name);
  }

  static bool IsAvailable(Func<BaseComponent, BaseComponent> resolve, AutomationBehavior behavior) {
    try {
      return resolve == null || resolve(behavior);
    } catch (Exception e) {
      DebugEx.Error("External capability availability check failed: {0}", e);
      return false;
    }
  }

  static BaseComponent ResolveSource(Func<BaseComponent, BaseComponent> resolve, AutomationBehavior behavior) {
    if (resolve == null) {
      return null;
    }
    try {
      var source = resolve(behavior);
      return source ? source : throw new ScriptError.BadStateError(behavior, "External component is not available.");
    } catch (ScriptError) {
      throw;
    } catch (Exception e) {
      DebugEx.Error("External component lookup failed: {0}", e);
      throw new ScriptError.BadStateError(behavior, "External component lookup failed.");
    }
  }

  static ScriptValue Read(Signal signal, BaseComponent source) {
    try {
      if (signal.Resolve != null && !source) {
        throw new ScriptError.BadValue("External signal source no longer exists.");
      }
      return signal.Read(source);
    } catch (ScriptError.RuntimeError) {
      throw;
    } catch (Exception e) {
      DebugEx.Error("External signal failed: {0}\n{1}", signal.Definition.ScriptName, e);
      throw new ScriptError.InternalError("External signal failed: " + signal.Definition.ScriptName);
    }
  }

  static bool Refresh(Watch watch) {
    var previousValue = watch.Value;
    var previousError = watch.Error;
    try {
      watch.Value = Read(watch.Signal, watch.Source);
      watch.Error = null;
    } catch (ScriptError.RuntimeError e) {
      watch.Error = e;
    }
    return !watch.Value.Equals(previousValue) || watch.Error?.Message != previousError?.Message;
  }

  IEnumerator ProcessUpdates() {
    while (_active && (_pendingHooks.Count > 0 || _pending.Count > 0)) {
      // Let the frame's game logic settle, as with the built-in weather signal.
      yield return new WaitForEndOfFrame();
      if (!AutomationService.AutomationSystemReady) {
        continue;
      }
      var hooks = _pendingHooks.ToArray();
      _pendingHooks.Clear();
      foreach (var hook in hooks) {
        hook.Dispatch(hook.Source);
      }
      var batch = _pending.ToArray();
      _pending.Clear();
      var changed = new List<Watch>();
      foreach (var watch in batch) {
        if (IsRegistered(watch) && Refresh(watch)) {
          changed.Add(watch);
        }
      }
      // Publish only after every dirty value in this batch has been sampled.
      foreach (var watch in changed) {
        if (IsRegistered(watch)) {
          var name = watch.Signal.Definition.ScriptName;
          if (watch.Tracker != null) {
            if (watch.Error != null) {
              watch.Tracker.TriggerSignalError(name);
            } else {
              watch.Tracker.TriggerSignalUpdate(name, watch.Value, forceNotify: true);
            }
          } else if (watch.Error != null) {
            _globalTracker.TriggerSignalError(name);
          } else {
            _globalTracker.TriggerSignalUpdate(name, watch.Value, forceNotify: true);
          }
        }
      }
    }
    _processing = null;
  }

  static bool IsRegistered(Watch watch) {
    return watch.Signal.Watches.TryGetValue(watch.Key, out var current) && ReferenceEquals(current, watch);
  }
}

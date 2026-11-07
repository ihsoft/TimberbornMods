// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.BaseComponentSystem;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Adapts source-API declarations to native script definitions and deferred signal observations.</summary>
sealed class ExternalScriptableComponent : ScriptableComponentBase, IUnloadableSingleton {
  sealed class Signal {
    public SignalDef Definition;
    public ExternalValueDefinition Value;
    public Func<BaseComponent, object> Read;
    public Func<BaseComponent, BaseComponent> Resolve;
    public readonly Dictionary<object, Watch> Watches = new();
  }

  sealed class ExternalAction {
    public ActionDef Definition;
    public ExternalValueDefinition Argument;
    public Action<BaseComponent, object> Execute;
    public Func<BaseComponent, BaseComponent> Resolve;
  }

  sealed class Watch {
    public Signal Signal;
    public object Key;
    public BaseComponent Source;
    public ReferenceManager References;
    public ScriptValue Value;
    public ScriptError.RuntimeError Error;
  }

  sealed class UpdateRunner : MonoBehaviour;

  /// <inheritdoc/>
  public override string Name => "Mods";

  /// <inheritdoc/>
  public override void Load() {
    base.Load();
    _runner = new GameObject("Automation.ExternalSignals").AddComponent<UpdateRunner>();
    _active = true;
  }

  /// <inheritdoc/>
  public void Unload() {
    _active = false;
    if (_runner) {
      UnityEngine.Object.Destroy(_runner.gameObject);
    }
    _runner = null;
    _processing = null;
    _pending.Clear();
    _signals.Clear();
    _signalIds.Clear();
    _actions.Clear();
  }

  /// <summary>Registers a V1 declaration without calling provider delegates; duplicates keep the first entry.</summary>
  public void Register(IDictionary<string, object> declaration) {
    var modId = (string)declaration["modId"];
    var id = (string)declaration["id"];
    var scriptName = MakeScriptName(modId, id);
    var kind = (string)declaration["kind"];
    if (kind == "signal" && _signals.ContainsKey(scriptName) || kind == "action" && _actions.ContainsKey(scriptName)) {
      DebugEx.Error("Duplicate external automation {0}: {1}. Keeping the first registration; ignoring this one.",
          kind, scriptName);
      return;
    }
    var name = Loc.T((string)declaration["nameLocKey"]);
    var provider = Loc.T((string)declaration["modNameLocKey"]);
    var type = (Type)declaration["type"];
    var resolve = (Func<BaseComponent, BaseComponent>)declaration["resolve"];
    var optionValues = (string[])declaration["optionValues"];
    var optionNames = (string[])declaration["optionNames"];
    var percent = declaration.TryGetValue("percent", out var format) && (bool)format;
    double? min = declaration.TryGetValue("min", out var lower) && lower != null ? Convert.ToDouble(lower) : null;
    double? max = declaration.TryGetValue("max", out var upper) && upper != null ? Convert.ToDouble(upper) : null;
    if (kind == "signal") {
      var value = new ExternalValueDefinition(type, Loc, optionValues, optionNames, min, max, percent);
      var signal = new Signal {
          Definition = new SignalDef {
              ScriptName = scriptName, DisplayName = name, ProviderName = provider,
              Scope = resolve == null ? SignalDef.ScopeEnum.Global : SignalDef.ScopeEnum.Building,
              Result = value.Definition,
          },
          Value = value, Read = (Func<BaseComponent, object>)declaration["read"], Resolve = resolve,
      };
      _signals.Add(scriptName, signal);
      _signalIds.Add((modId, id), signal);
    } else if (kind == "action") {
      var argument = type == null ? null : new ExternalValueDefinition(
          type, Loc, optionValues, optionNames, min, max, percent);
      _actions.Add(scriptName, new ExternalAction {
          Definition = new ActionDef {
              ScriptName = scriptName, DisplayName = name, ProviderName = provider,
              Arguments = argument == null ? [] : [argument.Definition],
          },
          Argument = argument,
          Execute = (Action<BaseComponent, object>)declaration["execute"], Resolve = resolve,
      });
    } else {
      throw new ArgumentException("Unknown capability kind.");
    }
    DebugEx.Info("Registered external automation {0}: {1}", kind, scriptName);
  }

  /// <summary>Only enqueues an existing observation; no provider delegates run in this call.</summary>
  public void UpdateSignal(string modId, string id, BaseComponent source) {
    if (!_active || !_signalIds.TryGetValue((modId, id), out var signal)) {
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
      return Read(signal, source);
    };
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var action = GetAction(name);
    var source = ResolveSource(action.Resolve, behavior);
    return args => {
      AssertActionArgsCount(name, args, action.Argument == null ? 0 : 1);
      try {
        if (action.Resolve != null && !source) {
          throw new ScriptError.BadValue("External action source no longer exists.");
        }
        var value = action.Argument == null ? null : action.Argument.FromScript(args[0]);
        action.Execute(source, value);
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
          Signal = signal, Source = source, Key = key, References = new ReferenceManager(ScriptingService),
      };
      Refresh(watch);
      signal.Watches.Add(key, watch);
    }
    watch.References.AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var signal = GetSignal(signalOperator.SignalName);
    var key = signal.Resolve == null ? GlobalSource : (object)host.Behavior.GameObject;
    if (!signal.Watches.TryGetValue(key, out var watch)) {
      return;
    }
    watch.References.RemoveSignal(signalOperator, host);
    if (watch.References.Signals.Count == 0) {
      signal.Watches.Remove(key);
      _pending.Remove(watch);
    }
  }

  static readonly object GlobalSource = new();
  readonly Dictionary<string, Signal> _signals = new();
  readonly Dictionary<(string ModId, string Id), Signal> _signalIds = new();
  readonly Dictionary<string, ExternalAction> _actions = new();
  readonly HashSet<Watch> _pending = new();
  UpdateRunner _runner;
  Coroutine _processing;
  bool _active;

  /// <summary>Persistent format: Mods.providerId.capabilityId, with case-sensitive IDs preserved verbatim.</summary>
  /// <remarks>
  /// Both IDs must match [A-Za-z][A-Za-z0-9]*. Invalid IDs are rejected; no encoding or decoding is performed.
  /// String option values are unchanged. Persisted names must remain stable across provider updates.
  /// </remarks>
  static string MakeScriptName(string modId, string id) {
    if (!Regex.IsMatch(modId ?? "", @"\A[A-Za-z][A-Za-z0-9]*\z")
        || !Regex.IsMatch(id ?? "", @"\A[A-Za-z][A-Za-z0-9]*\z")) {
      throw new ArgumentException("Provider and capability IDs must match [A-Za-z][A-Za-z0-9]*.");
    }
    return "Mods." + modId + "." + id;
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
      return signal.Value.ToScript(signal.Read(source));
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
    while (_active && _pending.Count > 0) {
      // Always leave the caller's stack, including updates produced by actions in the previous pass.
      yield return null;
      if (!AutomationService.AutomationSystemReady) {
        continue;
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
          watch.References.TriggerSignalUpdate(watch.Signal.Definition.ScriptName);
        }
      }
    }
    _processing = null;
  }

  static bool IsRegistered(Watch watch) {
    return watch.Signal.Watches.TryGetValue(watch.Key, out var current) && ReferenceEquals(current, watch);
  }
}

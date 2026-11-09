// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Timberborn.SingletonSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Owns runtime setter patches that queue signal updates without reading values or executing rules.</summary>
sealed class PropertySetterHooks : IUnloadableSingleton {
  const string PatchId = "IgorZ.Automation.ConfiguredSetters";

  /// <summary>One signal's notification callback, shared by all setters that can change it.</summary>
  public sealed class Observer {
    /// <summary>Provider-qualified identifier for diagnostics.</summary>
    public string Name;
    /// <summary>Filters the source and queues its signal for sampling in the current end-of-frame batch.</summary>
    public Action<object> Notify;
    /// <summary>Set only after registration; cleared on failure and unload.</summary>
    public bool Active;
  }

  sealed class Hook {
    /// <summary>Signal bindings sharing this setter, including inactive registrations.</summary>
    public readonly List<Observer> Observers = [];
    /// <summary>Stable callback identity used to coalesce calls for this hook and source.</summary>
    public readonly Action<object> Dispatch;

    /// <summary>Creates the callback once, without allocating a delegate on each setter call.</summary>
    public Hook() {
      Dispatch = Process;
    }

    void Process(object instance) {
      // A failing callback can detach itself. Reverse traversal keeps the other observers intact.
      for (var i = Observers.Count - 1; i >= 0; i--) {
        var observer = Observers[i];
        if (!observer.Active) {
          continue;
        }
        try {
          observer.Notify(instance);
        } catch (Exception e) {
          Disable(observer, e);
        }
      }
    }
  }

  /// <summary>Install before game services Load; an observer remains inactive until its signal is registered.</summary>
  public void Add(MethodInfo[] setters, Observer observer) {
    try {
      foreach (var setter in setters.Distinct()) {
        if (!Watches.TryGetValue(setter, out var hook)) {
          hook = new Hook();
          Watches.Add(setter, hook);
          Patcher.Patch(setter, postfix: new HarmonyMethod(typeof(PropertySetterHooks), nameof(Postfix)));
          DebugEx.Info("[Automation Hooks] Installed {0}.{1}", setter.DeclaringType?.FullName, setter.Name);
        }
        hook.Observers.Add(observer);
      }
    } catch {
      RemoveObserver(observer);
      throw;
    }
  }

  /// <summary>Detaches a rejected signal and removes only patches no other observer still needs.</summary>
  public void Remove(Observer observer) {
    RemoveObserver(observer);
  }

  /// <inheritdoc/>
  public void Unload() {
    _component = null;
    foreach (var hook in Watches.Values) {
      foreach (var observer in hook.Observers) {
        observer.Active = false;
      }
    }
    foreach (var method in Watches.Keys.ToArray()) {
      RemovePatch(method);
    }
  }

  static readonly Harmony Patcher = new(PatchId);
  static readonly Dictionary<MethodBase, Hook> Watches = new();
  static ExternalScriptableComponent _component;

  PropertySetterHooks(ExternalScriptableComponent component) {
    Unload();
    _component = component;
  }

  static void RemovePatch(MethodBase method) {
    Watches[method].Observers.Clear();
    try {
      Patcher.Unpatch(method, HarmonyPatchType.All, PatchId);
      Watches.Remove(method);
      DebugEx.Info("[Automation Hooks] Removed {0}.{1}", method.DeclaringType?.FullName, method.Name);
    } catch (Exception e) {
      // Keep the empty entry so unload can retry, without retaining provider callbacks or instances.
      DebugEx.Error("[Automation Hooks] Cannot remove patch for {0}: {1}", method, e);
    }
  }

  static void RemoveObserver(Observer observer) {
    observer.Active = false;
    foreach (var method in Watches.Keys.ToArray()) {
      var observers = Watches[method].Observers;
      observers.Remove(observer);
      if (observers.Count == 0) {
        RemovePatch(method);
      }
    }
  }

  static void Postfix(object __instance, MethodBase __originalMethod) {
    if (_component != null && Watches.TryGetValue(__originalMethod, out var hook) && hook.Observers.Count > 0) {
      _component.ScheduleHook(hook.Dispatch, __instance);
    }
  }

  static void Disable(Observer observer, Exception error) {
    RemoveObserver(observer);
    DebugEx.Error("[Automation Hooks] Disabled '{0}' after callback failure: {1}", observer.Name, error);
  }
}

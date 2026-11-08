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

/// <summary>Owns runtime setter patches and compares values without executing rules on the setter's stack.</summary>
sealed class PropertySetterHooks : IUnloadableSingleton {
  const string PatchId = "IgorZ.Automation.ConfiguredSetters";

  /// <summary>One signal's projection and callbacks, shared by all setters that can change it.</summary>
  public sealed class Observer {
    /// <summary>Provider-qualified identifier for diagnostics.</summary>
    public string Name;
    /// <summary>Pure getter; evaluated synchronously around an observed setter.</summary>
    public Func<object, object> Read;
    /// <summary>Filters other worlds, dead components and sources without listeners.</summary>
    public Func<object, bool> IsObserved;
    /// <summary>Queues a deferred signal update after an actual value change.</summary>
    public Action<object> Notify;
    /// <summary>Set only after registration; cleared on failure and unload.</summary>
    public bool Active;
  }

  readonly record struct Snapshot(Observer Observer, object Value);

  /// <summary>Install before game services Load; an observer remains inactive until its signal is registered.</summary>
  public void Add(MethodInfo[] setters, Observer observer) {
    try {
      foreach (var setter in setters.Distinct()) {
        if (!Watches.TryGetValue(setter, out var observers)) {
          observers = [];
          Watches.Add(setter, observers);
          Patcher.Patch(setter, prefix: new HarmonyMethod(typeof(PropertySetterHooks), nameof(Prefix)),
              postfix: new HarmonyMethod(typeof(PropertySetterHooks), nameof(Postfix)));
          DebugEx.Info("[Automation Hooks] Installed {0}.{1}", setter.DeclaringType?.FullName, setter.Name);
        }
        observers.Add(observer);
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
    foreach (var observers in Watches.Values) {
      foreach (var observer in observers) {
        observer.Active = false;
      }
    }
    foreach (var method in Watches.Keys.ToArray()) {
      RemovePatch(method);
    }
  }

  static readonly Harmony Patcher = new(PatchId);
  static readonly Dictionary<MethodBase, List<Observer>> Watches = new();

  PropertySetterHooks() {
    Unload();
  }

  static void RemovePatch(MethodBase method) {
    Watches[method].Clear();
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
      var observers = Watches[method];
      observers.Remove(observer);
      if (observers.Count == 0) {
        RemovePatch(method);
      }
    }
  }

  static void Prefix(object __instance, MethodBase __originalMethod, out List<Snapshot> __state) {
    __state = null;
    if (!Watches.TryGetValue(__originalMethod, out var observers)) {
      return;
    }
    // A failing reader can detach itself from this list. Reverse traversal keeps the other observers intact.
    for (var i = observers.Count - 1; i >= 0; i--) {
      var observer = observers[i];
      if (!observer.Active) {
        continue;
      }
      try {
        if (observer.IsObserved(__instance)) {
          var value = observer.Read(__instance);
          __state ??= [];
          __state.Add(new Snapshot(observer, value));
        }
      } catch (Exception e) {
        Disable(observer, e);
      }
    }
  }

  static void Postfix(object __instance, List<Snapshot> __state) {
    if (__state == null) {
      return;
    }
    foreach (var snapshot in __state) {
      var observer = snapshot.Observer;
      if (!observer.Active) {
        continue;
      }
      try {
        if (!Equals(snapshot.Value, observer.Read(__instance))) {
          observer.Notify(__instance);
        }
      } catch (Exception e) {
        Disable(observer, e);
      }
    }
  }

  static void Disable(Observer observer, Exception error) {
    RemoveObserver(observer);
    DebugEx.Error("[Automation Hooks] Disabled '{0}' after callback failure: {1}", observer.Name, error);
  }
}

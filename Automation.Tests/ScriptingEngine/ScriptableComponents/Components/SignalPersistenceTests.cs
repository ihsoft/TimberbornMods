using System;
using System.Collections.Generic;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Automation.Tests;

static class SignalPersistenceTests {
  public static readonly (string Name, Action Run)[] Cases = [
      ("Global signals restore without reading and defer/coalesce subsequent updates", RestoresGlobalSignals),
      ("Global signals retain valid saved values across errors and notify recovery", PreservesValuesAcrossErrors),
      ("Building hook signals restore independent baselines without reading", RestoresBuildingSignals),
      ("Global tracker round-trips strings, booleans and exact numeric values", RoundTripsGlobalValues),
      ("Global tracker omits empty and unset-only saves while preserving zero", OmitsEmptyGlobalSaves),
      ("Global signal component loads legacy null and empty saved collections", LoadsEmptyGlobalSaves),
      ("Setter calls coalesce before dispatch and sample the latest value once", CoalescesHookCalls),
      ("Hook queue keeps separate hook and source identities without provider equality", PreservesHookIdentities),
      ("Hook queue skips unobserved signals and discards work on unload", SkipsUnobservedAndUnloadedHooks),
  ];

  sealed class Store : ISingletonLoader, ISingletonSaver {
    public readonly Dictionary<string, IObjectSaver> Values = new();

    public IObjectSaver GetSingleton(SingletonKey key) {
      if (!Values.TryGetValue(key.Name, out var saver)) {
        Values.Add(key.Name, saver = new IObjectSaver());
      }
      return saver;
    }

    public bool TryGetSingleton(SingletonKey key, out IObjectLoader loader) {
      loader = Values.TryGetValue(key.Name, out var saver) ? new IObjectLoader(saver.Values) : null;
      return loader != null;
    }
  }

  sealed class Listener : ISignalListener {
    public AutomationBehavior Behavior { get; } = new();
    public int Calls;
    public void OnValueChanged(string name) => Calls++;
  }

  sealed class IdentityOnlySource {
    public override bool Equals(object obj) => throw new InvalidOperationException("Provider equality must not run");
    public override int GetHashCode() => throw new InvalidOperationException("Provider hashing must not run");
  }

  sealed class GlobalTracker(ReferenceManager references)
      : AbstractGlobalStatusTracker(new SingletonKey("TestSignals"), references);

  static void RestoresGlobalSignals() {
    var store = new Store();
    var value = 1;
    var reads = 0;
    var unavailable = false;
    ScriptValue Read(BaseComponent _) {
      reads++;
      if (unavailable) {
        throw new ScriptError.BadValue("Not ready");
      }
      return ScriptValue.FromInt(value);
    }
    var component = CreateComponent(store, Read);
    Subscribe(component, new Listener());
    component.Save(store);
    component.Unload();
    Assert.Equal(1, store.Values.Count);

    unavailable = true;
    reads = 0;
    component = CreateComponent(store, Read);
    var first = new Listener();
    var second = new Listener();
    Subscribe(component, first);
    Subscribe(component, first);
    Subscribe(component, second);
    Assert.Equal(0, reads);
    Assert.Equal(ScriptValue.FromInt(1), component.GetSignalSource("Mods.Test.Value", first.Behavior)());
    Assert.Equal(0, first.Calls);

    unavailable = false;
    component.UpdateSignal("Mods.Test.Value", null);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(0, first.Calls);
    value = 2;
    for (var i = 0; i < 100; i++) {
      component.UpdateSignal("Mods.Test.Value", null);
    }
    Assert.Equal(0, first.Calls);
    Assert.Equal(1, reads);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(2, reads);
    Assert.Equal(1, first.Calls);
    Assert.Equal(1, second.Calls);
    component.Unload();
  }

  static void PreservesValuesAcrossErrors() {
    var store = new Store();
    var failed = false;
    var component = CreateComponent(store,
        _ => failed ? throw new ScriptError.BadValue("Not ready") : ScriptValue.FromInt(7));
    var listener = new Listener();
    Subscribe(component, listener);
    failed = true;
    component.UpdateSignal("Mods.Test.Value", null);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(1, listener.Calls);
    Assert.Throws<ScriptError.BadValue>(() => component.GetSignalSource("Mods.Test.Value", listener.Behavior)());
    component.Save(store);
    failed = false;
    component.UpdateSignal("Mods.Test.Value", null);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(2, listener.Calls);
    component.Unload();

    component = CreateComponent(store, _ => throw new InvalidOperationException("Must not read on restore"));
    Subscribe(component, new Listener());
    Assert.Equal(ScriptValue.FromInt(7), component.GetSignalSource("Mods.Test.Value", listener.Behavior)());
    component.Unload();
  }

  static void RestoresBuildingSignals() {
    Activator.CreateInstance(typeof(StaticBindings), BindingFlags.Instance | BindingFlags.NonPublic,
        null, [new TestContainer()], null);
    var component = CreateComponent(new Store(), _ => ScriptValue.FromInt(99), building: true);
    var first = new Listener();
    var second = new Listener();
    first.Behavior.GetOrCreate<ExternalScriptableComponent.SignalTracker>()
        .InitializeSignalValue("Mods.Test.Value", ScriptValue.FromInt(3));
    second.Behavior.GetOrCreate<ExternalScriptableComponent.SignalTracker>()
        .InitializeSignalValue("Mods.Test.Value", ScriptValue.FromInt(8));
    Subscribe(component, first);
    Subscribe(component, second);
    var firstSave = SaveBuilding(first);
    var secondSave = SaveBuilding(second);
    component.Unload();

    var reads = 0;
    component = CreateComponent(new Store(), _ => { reads++; return ScriptValue.FromInt(4); }, building: true);
    first = RestoreBuilding(firstSave);
    second = RestoreBuilding(secondSave);
    Subscribe(component, first);
    Subscribe(component, second);
    Assert.Equal(0, reads);
    Assert.Equal(ScriptValue.FromInt(3), component.GetSignalSource("Mods.Test.Value", first.Behavior)());
    Assert.Equal(ScriptValue.FromInt(8), component.GetSignalSource("Mods.Test.Value", second.Behavior)());
    component.UpdateSignal("Mods.Test.Value", first.Behavior);
    Assert.Equal(0, first.Calls);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(1, first.Calls);
    Assert.Equal(0, second.Calls);
    Assert.Equal(1, reads);
    component.Unload();
  }

  static void RoundTripsGlobalValues() {
    var service = TestScripting.CreateService();
    var tracker = new GlobalTracker(new ReferenceManager(service));
    ScriptValue[] values = [ScriptValue.FromString("Season"), ScriptValue.FromBool(true), ScriptValue.Of(29)];
    for (var i = 0; i < values.Length; i++) {
      tracker.SetSignalValue("Signal" + i, values[i]);
    }
    var store = new Store();
    tracker.Save(store);
    var restored = new GlobalTracker(new ReferenceManager(service));
    restored.Load(store);
    for (var i = 0; i < values.Length; i++) {
      Assert.Equal(values[i], restored.GetOrInitialize("Signal" + i, () => throw new Exception("Unexpected read")));
    }
  }

  static void OmitsEmptyGlobalSaves() {
    var service = TestScripting.CreateService();
    var tracker = new GlobalTracker(new ReferenceManager(service));
    var emptySave = new Store();
    tracker.Save(emptySave);
    Assert.Equal(0, emptySave.Values.Count);

    tracker.SetSignalValue("Unavailable", ScriptValue.InvalidValue);
    var unsetSave = new Store();
    tracker.Save(unsetSave);
    Assert.Equal(0, unsetSave.Values.Count);

    tracker.SetSignalValue("Zero", ScriptValue.FromInt(0));
    var validSave = new Store();
    tracker.Save(validSave);
    Assert.Equal(1, validSave.Values.Count);
    var restored = new GlobalTracker(new ReferenceManager(service));
    restored.Load(validSave);
    Assert.False(restored.TryGetSignalValue("Unavailable", out _));
    Assert.True(restored.TryGetSignalValue("Zero", out var value));
    Assert.Equal(ScriptValue.FromInt(0), value);
  }

  static void LoadsEmptyGlobalSaves() {
    SavedSignalValues[] states = [null, new(), new() { SignalValues = [] }];
    foreach (var state in states) {
      var store = new Store();
      // The serializer stub supplies the deserialized shape, including the legacy null array.
      store.GetSingleton(new SingletonKey("IgorZ.Automation.ConfiguredSignals"))
          .Set(new PropertyKey<string>("SavedSignals"), StringProtoSerializer.Serialize(state));
      var reads = 0;
      var component = CreateComponent(store, _ => {
        reads++;
        return ScriptValue.FromInt(7);
      });
      Assert.Equal(0, reads);
      var emptySave = new Store();
      component.Save(emptySave);
      Assert.Equal(0, emptySave.Values.Count);

      var listener = new Listener();
      Subscribe(component, listener);
      Assert.Equal(1, reads);
      Assert.Equal(0, listener.Calls);
      Assert.Equal(ScriptValue.FromInt(7), component.GetSignalSource("Mods.Test.Value", listener.Behavior)());
      var validSave = new Store();
      component.Save(validSave);
      Assert.Equal(1, validSave.Values.Count);
      component.Unload();
    }
  }

  static void CoalescesHookCalls() {
    var value = 1;
    var reads = 0;
    var dispatches = 0;
    var component = CreateComponent(new Store(), _ => {
      reads++;
      return ScriptValue.FromInt(value);
    });
    var listener = new Listener();
    Subscribe(component, listener);
    Action<object> firstHook = _ => {
      dispatches++;
      component.UpdateSignal("Mods.Test.Value", null);
    };
    Action<object> secondHook = _ => {
      dispatches++;
      component.UpdateSignal("Mods.Test.Value", null);
      value = 3;
    };
    for (var i = 0; i < 100; i++) {
      component.ScheduleHook(firstHook, null);
      component.ScheduleHook(secondHook, null);
    }
    Assert.Equal(0, dispatches);
    Assert.Equal(1, reads);
    Assert.Equal(0, listener.Calls);
    Assert.Equal(1, MonoBehaviour.QueuedCoroutineCount);
    value = 2;
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(2, dispatches);
    Assert.Equal(2, reads);
    Assert.Equal(1, listener.Calls);
    Assert.Equal(ScriptValue.FromInt(3), component.GetSignalSource("Mods.Test.Value", listener.Behavior)());

    for (var i = 0; i < 100; i++) {
      component.ScheduleHook(firstHook, null);
    }
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(3, dispatches);
    Assert.Equal(3, reads);
    Assert.Equal(1, listener.Calls);
    component.Unload();
  }

  static void PreservesHookIdentities() {
    var component = CreateComponent(new Store(), _ => throw new Exception("Unexpected signal read"));
    var firstSource = new IdentityOnlySource();
    var secondSource = new IdentityOnlySource();
    var firstCalls = new List<object>();
    var secondCalls = new List<object>();
    Action<object> firstHook = firstCalls.Add;
    Action<object> secondHook = secondCalls.Add;
    for (var i = 0; i < 100; i++) {
      component.ScheduleHook(firstHook, firstSource);
      component.ScheduleHook(firstHook, secondSource);
      component.ScheduleHook(firstHook, null);
      component.ScheduleHook(secondHook, firstSource);
    }
    Assert.Equal(0, firstCalls.Count);
    Assert.Equal(0, secondCalls.Count);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(3, firstCalls.Count);
    Assert.Equal(1, firstCalls.FindAll(source => ReferenceEquals(source, firstSource)).Count);
    Assert.Equal(1, firstCalls.FindAll(source => ReferenceEquals(source, secondSource)).Count);
    Assert.Equal(1, firstCalls.FindAll(source => source == null).Count);
    Assert.Equal(1, secondCalls.Count);
    Assert.Same(firstSource, secondCalls[0]);
    component.Unload();
  }

  static void SkipsUnobservedAndUnloadedHooks() {
    var reads = 0;
    var dispatches = 0;
    var component = CreateComponent(new Store(), _ => {
      reads++;
      return ScriptValue.FromInt(1);
    });
    Action<object> hook = _ => {
      dispatches++;
      component.UpdateSignal("Mods.Test.Value", null);
    };
    component.ScheduleHook(hook, null);
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(1, dispatches);
    Assert.Equal(0, reads);
    Subscribe(component, new Listener());
    component.ScheduleHook(hook, null);
    component.Unload();
    MonoBehaviour.RunQueuedCoroutines();
    Assert.Equal(1, dispatches);
    Assert.Equal(1, reads);
    component.ScheduleHook(hook, null);
    Assert.Equal(0, MonoBehaviour.QueuedCoroutineCount);
  }

  static ExternalScriptableComponent CreateComponent(
      Store store, Func<BaseComponent, ScriptValue> read, bool building = false) {
    MonoBehaviour.ClearQueuedCoroutines();
    var service = TestScripting.CreateService();
    var component = (ExternalScriptableComponent)Activator.CreateInstance(typeof(ExternalScriptableComponent),
        BindingFlags.Instance | BindingFlags.NonPublic, null, [store], null);
    component.InjectDependencies(new FakeLoc(), service);
    component.Load();
    component.RegisterSignal(new SignalDef {
        ScriptName = "Mods.Test.Value", DisplayName = "Test",
        Scope = building ? SignalDef.ScopeEnum.Building : SignalDef.ScopeEnum.Global,
        Result = new ValueDef { ValueType = ScriptValue.TypeEnum.Number },
    }, read, building ? source => source : null);
    return component;
  }

  static void Subscribe(ExternalScriptableComponent component, Listener listener) {
    var signal = SignalOperator.Create(new ExpressionContext { ScriptHost = listener.Behavior }, "Mods.Test.Value");
    component.RegisterSignalChangeCallback(signal, listener);
  }

  static TestEntitySaver SaveBuilding(Listener listener) {
    var saver = new TestEntitySaver();
    listener.Behavior.GetOrCreate<ExternalScriptableComponent.SignalTracker>().Save(saver);
    return saver;
  }

  static Listener RestoreBuilding(TestEntitySaver saver) {
    var listener = new Listener();
    var key = new ComponentKey(typeof(ExternalScriptableComponent.SignalTracker).FullName);
    var loader = new TestEntityLoader();
    loader.SetComponent(key, new IObjectLoader(saver.Components[key.Name].Values));
    listener.Behavior.GetOrCreate<ExternalScriptableComponent.SignalTracker>().Load(loader);
    return listener;
  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;

namespace Automation.Tests;

static class AutomationBehaviorTests {
  public static void GetOrCreateCachesComponent() {
    var harness = new Harness();

    var first = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();
    var second = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();

    Assert.Same(first, second);
    Assert.True(harness.Behavior.TryGetDynamicComponent<RecordingDynamicComponent>(out var found));
    Assert.Same(first, found);
    Assert.Equal(1, harness.Container.CreatedComponents.Count);
  }

  public static void GetOrThrowReportsMissingComponent() {
    var harness = new Harness();

    Assert.False(harness.Behavior.TryGetDynamicComponent<RecordingDynamicComponent>(out var component));
    Assert.Equal(null, component);
    Assert.Throws<InvalidOperationException>(() => harness.Behavior.GetOrThrow<RecordingDynamicComponent>());
  }

  public static void GetOrCreateCallsAwake() {
    var harness = new Harness();

    var component = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();

    Assert.Equal("Awake", component.EventsText);
    Assert.Same(harness.Behavior, component.AutomationBehavior);
  }

  public static void GetOrCreateAfterFinished() {
    var harness = new Harness();

    harness.Behavior.OnEnterFinishedState();
    var component = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();

    Assert.Equal("Awake,EnterFinished", component.EventsText);
  }

  public static void GetOrCreateAfterInitialized() {
    var harness = new Harness();

    harness.Behavior.InitializeEntity();
    var component = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();

    Assert.Equal("Awake,InitializeEntity", component.EventsText);
  }

  public static void GetOrCreateAfterFinishedAndInitialized() {
    var harness = new Harness();

    harness.Behavior.OnEnterFinishedState();
    harness.Behavior.InitializeEntity();
    var component = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();

    Assert.Equal("Awake,EnterFinished,InitializeEntity", component.EventsText);
  }

  public static void ForwardsLifecycleCallbacks() {
    var harness = new Harness();
    var component = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();
    component.ClearEvents();

    harness.Behavior.OnEnterFinishedState();
    harness.Behavior.InitializeEntity();
    harness.Behavior.OnExitFinishedState();

    Assert.Equal("EnterFinished,InitializeEntity,ExitFinished", component.EventsText);
  }

  public static void DeleteEntityForwardsToComponents() {
    var harness = new Harness();
    var component = harness.Behavior.GetOrCreate<RecordingDynamicComponent>();
    component.ClearEvents();

    harness.Behavior.DeleteEntity();

    Assert.Equal("DeleteEntity", component.EventsText);
  }

  sealed class Harness {
    public readonly RecordingContainer Container = new();
    public readonly AutomationBehavior Behavior = new();

    public Harness() {
      SetDependencyContainer(Container);
      Behavior.Name = "TestBehavior";
      Behavior.SetComponent(new BlockObject());
      Behavior.InjectDependencies(new AutomationService());
      Behavior.Awake();
    }

    static void SetDependencyContainer(IContainer container) {
      var constructor = typeof(StaticBindings).GetConstructor(
          BindingFlags.Instance | BindingFlags.NonPublic,
          null,
          [typeof(IContainer)],
          null);
      constructor.Invoke([container]);
    }
  }

  sealed class RecordingContainer : IContainer {
    public readonly List<object> CreatedComponents = [];

    public object GetInstance(Type type) {
      var component = Activator.CreateInstance(type);
      CreatedComponents.Add(component);
      return component;
    }
  }

  sealed class RecordingDynamicComponent : AbstractDynamicComponent, IAwakableComponent,
                                          IFinishedStateListener, IInitializableEntity, IDeletableEntity {
    readonly List<string> _events = [];

    public string EventsText => string.Join(",", _events);

    public void ClearEvents() {
      _events.Clear();
    }

    public void Awake() {
      _events.Add("Awake");
    }

    public void OnEnterFinishedState() {
      _events.Add("EnterFinished");
    }

    public void OnExitFinishedState() {
      _events.Add("ExitFinished");
    }

    public void InitializeEntity() {
      _events.Add("InitializeEntity");
    }

    public void DeleteEntity() {
      _events.Add("DeleteEntity");
    }
  }
}

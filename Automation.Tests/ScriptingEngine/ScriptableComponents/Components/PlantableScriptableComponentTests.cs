using System;
using System.Collections.Generic;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Localization;
using Timberborn.Multithreading;
using Timberborn.Planting;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Automation.Tests;

static class PlantableScriptableComponentTests {
  public static void ExposesSignalForPlantingBuilding() {
    var component = CreateComponent(new PlantingService(), new TestParallelizer());
    var behavior = CreateBehavior(new PlantingService());

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("Plantable.Ready", signalNames[0]);
  }

  public static void HidesSignalForMissingPlantingComponents() {
    var component = CreateComponent(new PlantingService(), new TestParallelizer());
    var noComponentsBehavior = new AutomationBehavior();
    var noCoordinatesBehavior = new AutomationBehavior();
    noCoordinatesBehavior.SetComponent(new PlantingSpotFinder());
    var noFinderBehavior = new AutomationBehavior();
    noFinderBehavior.SetComponent(new InRangePlantingCoordinates());

    Assert.Equal(0, component.GetSignalNamesForBuilding(noComponentsBehavior).Length);
    Assert.Equal(0, component.GetSignalNamesForBuilding(noCoordinatesBehavior).Length);
    Assert.Equal(0, component.GetSignalNamesForBuilding(noFinderBehavior).Length);
  }

  public static void BuildsReadySignalDefinition() {
    var component = CreateComponent(new PlantingService(), new TestParallelizer());
    var behavior = CreateBehavior(new PlantingService());

    var signalDef = component.GetSignalDefinition("Plantable.Ready", behavior);

    Assert.Equal("Plantable.Ready", signalDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.Plantable.Signal.SpotsReady", signalDef.DisplayName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, signalDef.Result.DisplayNumericFormat);
    Assert.Equal((0, float.NaN), signalDef.Result.DisplayNumericFormatRange);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent(new PlantingService(), new TestParallelizer());
    var behavior = CreateBehavior(new PlantingService());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalSource("Plantable.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("Plantable.Missing", behavior));
  }

  public static void UpdatesReadySpotsThroughParallelTickAndNotifiesListeners() {
    var plantingService = new PlantingService();
    var parallelizer = new TestParallelizer();
    var component = CreateComponent(plantingService, parallelizer);
    var behavior = CreateBehavior(plantingService);
    var coordinates = behavior.GetComponent<InRangePlantingCoordinates>();
    var finder = behavior.GetComponent<PlantingSpotFinder>();
    var first = new Vector3Int(1, 2, 0);
    var blocked = new Vector3Int(2, 2, 0);
    var reserved = new Vector3Int(3, 2, 0);
    var listener = new TestSignalListener(behavior);
    coordinates.SetCoordinates(first, blocked, reserved);
    plantingService.SetSpot(first, "Pine");
    plantingService.SetSpot(blocked, "Birch");
    plantingService.SetSpot(reserved, "Oak", hasSpot: false);
    plantingService._reservedCoordinates.Add(reserved);
    finder.SetCanPlantAt(blocked, canPlant: false);
    try {
      Time.timeScale = 1f;

      component.RegisterSignalChangeCallback(Signal("Plantable.Ready", behavior), listener);
      behavior.OnEnterFinishedState();
      component.StartParallelTick();

      Assert.Equal(1, parallelizer.PendingTasks);
      Assert.Equal(0, component.GetSignalSource("Plantable.Ready", behavior)().AsInt);

      parallelizer.RunScheduledTasks();

      Assert.Equal(0, component.GetSignalSource("Plantable.Ready", behavior)().AsInt);

      component.Tick();

      Assert.Equal(2, component.GetSignalSource("Plantable.Ready", behavior)().AsInt);
      Assert.Equal(1, listener.Calls);
      Assert.Equal("Plantable.Ready", listener.LastSignalName);
    } finally {
      Time.timeScale = 1f;
    }
  }

  public static void UpdatesReadySpotsImmediatelyWhenPaused() {
    var plantingService = new PlantingService();
    var component = CreateComponent(plantingService, new TestParallelizer());
    var behavior = CreateBehavior(plantingService);
    var coordinates = behavior.GetComponent<InRangePlantingCoordinates>();
    var range = behavior.GetComponent<BuildingTerrainRange>();
    var first = new Vector3Int(1, 2, 0);
    var second = new Vector3Int(2, 2, 0);
    var listener = new TestSignalListener(behavior);
    coordinates.SetCoordinates(first);
    plantingService.SetSpot(first, "Pine");
    plantingService.SetSpot(second, "Birch");

    try {
      Time.timeScale = 0f;

      component.RegisterSignalChangeCallback(Signal("Plantable.Ready", behavior), listener);
      behavior.OnEnterFinishedState();
      MonoBehaviour.RunQueuedCoroutines();

      Assert.Equal(1, component.GetSignalSource("Plantable.Ready", behavior)().AsInt);
      Assert.Equal(1, listener.Calls);

      coordinates.SetCoordinates(first, second);
      range.RaiseRangeChanged();
      MonoBehaviour.RunQueuedCoroutines();

      Assert.Equal(2, component.GetSignalSource("Plantable.Ready", behavior)().AsInt);
      Assert.Equal(2, listener.Calls);
    } finally {
      Time.timeScale = 1f;
      MonoBehaviour.ClearQueuedCoroutines();
    }
  }

  public static void UnregisterRemovesTrackerFromParallelUpdates() {
    var plantingService = new PlantingService();
    var parallelizer = new TestParallelizer();
    var component = CreateComponent(plantingService, parallelizer);
    var behavior = CreateBehavior(plantingService);
    var listener = new TestSignalListener(behavior);
    var signal = Signal("Plantable.Ready", behavior);

    component.RegisterSignalChangeCallback(signal, listener);
    component.UnregisterSignalChangeCallback(signal, listener);
    component.StartParallelTick();

    Assert.Equal(0, parallelizer.PendingTasks);
  }

  static PlantableScriptableComponent CreateComponent(PlantingService plantingService, TestParallelizer parallelizer) {
    SetDependencyContainer(CreateContainer(plantingService));
    var constructor = typeof(PlantableScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IParallelizer)],
        null);
    var component = (PlantableScriptableComponent)constructor.Invoke([parallelizer]);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static AutomationBehavior CreateBehavior(PlantingService plantingService) {
    SetDependencyContainer(CreateContainer(plantingService));
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new BlockObject());
    behavior.SetComponent(new BuildingTerrainRange());
    behavior.SetComponent(new InRangePlantingCoordinates());
    behavior.SetComponent(new PlantingSpotFinder());
    behavior.InjectDependencies(new AutomationService());
    behavior.Awake();
    return behavior;
  }

  static TestContainer CreateContainer(PlantingService plantingService) {
    var container = new TestContainer();
    container.Register(() => new EventBus());
    container.Register(() => plantingService);
    return container;
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }

  sealed class TestSignalListener(AutomationBehavior behavior) : ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
    }
  }

  sealed class TestParallelizer : IParallelizer {
    readonly List<Action> _tasks = [];

    public long LastTaskTimestamp => 0;
    public int NumberOfThreads => 1;
    public int PendingTasks => _tasks.Count;

    public ParallelizerHandle Schedule<T>(in T task) where T : struct, IParallelizerSingleTask {
      var taskCopy = task;
      _tasks.Add(() => taskCopy.Run());
      return new ParallelizerHandle();
    }

    public ParallelizerHandle Schedule<T>(in T task, ParallelizerHandle dependency)
        where T : struct, IParallelizerSingleTask {
      return Schedule(task);
    }

    public ParallelizerHandle Schedule<T>(in T task, ReadOnlySpan<ParallelizerHandle> dependencies)
        where T : struct, IParallelizerSingleTask {
      return Schedule(task);
    }

    public ParallelizerHandle Schedule<T>(int fromInclusive, int toExclusive, int batchSize, in T task)
        where T : struct, IParallelizerLoopTask {
      return new ParallelizerHandle();
    }

    public ParallelizerHandle Schedule<T>(
        int fromInclusive, int toExclusive, int batchSize, in T task, ParallelizerHandle dependency)
        where T : struct, IParallelizerLoopTask {
      return new ParallelizerHandle();
    }

    public ParallelizerHandle Schedule<T>(
        int fromInclusive, int toExclusive, int batchSize, in T task, ReadOnlySpan<ParallelizerHandle> dependencies)
        where T : struct, IParallelizerLoopTask {
      return new ParallelizerHandle();
    }

    public void StartScheduling() {
    }

    public void StopScheduling() {
    }

    public void Wait() {
    }

    public void ThrowIfAnyPendingTasks() {
    }

    public void RunScheduledTasks() {
      var tasks = _tasks.ToArray();
      _tasks.Clear();
      foreach (var task in tasks) {
        task();
      }
    }
  }
}

using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Forestry;
using Timberborn.Localization;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.SingletonSystem;
using Timberborn.Yielding;
using UnityEngine;

namespace Automation.Tests;

static class CollectableScriptableComponentTests {
  public static void ExposesSignalForCollectingBuilding() {
    var component = CreateComponent();
    var behavior = CreateBehavior(new BlockService());

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("Collectable.Ready", signalNames[0]);
  }

  public static void HidesSignalForMissingCollectingComponents() {
    var component = CreateComponent();
    var noComponentsBehavior = new AutomationBehavior();
    var noRangeBehavior = new AutomationBehavior();
    noRangeBehavior.SetComponent(new YieldRemovingBuilding());
    var noCollectorBehavior = new AutomationBehavior();
    noCollectorBehavior.SetComponent(new BuildingTerrainRange());

    Assert.Equal(0, component.GetSignalNamesForBuilding(noComponentsBehavior).Length);
    Assert.Equal(0, component.GetSignalNamesForBuilding(noRangeBehavior).Length);
    Assert.Equal(0, component.GetSignalNamesForBuilding(noCollectorBehavior).Length);
  }

  public static void BuildsReadySignalDefinition() {
    var component = CreateComponent();
    var behavior = CreateBehavior(new BlockService());

    var signalDef = component.GetSignalDefinition("Collectable.Ready", behavior);

    Assert.Equal("Collectable.Ready", signalDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.Collectable.Signal.CollectableReady", signalDef.DisplayName);
    Assert.Equal(ScriptValue.TypeEnum.Number, signalDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, signalDef.Result.DisplayNumericFormat);
    Assert.Equal((0, float.NaN), signalDef.Result.DisplayNumericFormatRange);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();
    var behavior = CreateBehavior(new BlockService());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalSource("Collectable.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("Collectable.Missing", behavior));
  }

  public static void TracksReadyYieldersAndNotifiesListeners() {
    var blockService = new BlockService();
    var component = CreateComponent(blockService);
    var behavior = CreateBehavior(blockService);
    var range = behavior.GetComponent<BuildingTerrainRange>();
    var collector = behavior.GetComponent<YieldRemovingBuilding>();
    var readyYielder = CreateYielderBlock(new Vector3Int(1, 2, 0), out var ready);
    var inactiveYielder = CreateYielderBlock(new Vector3Int(2, 2, 0), out var inactive);
    var deadYielder = CreateYielderBlock(new Vector3Int(3, 2, 0), out var dead);
    var disallowedYielder = CreateYielderBlock(new Vector3Int(4, 2, 0), out var disallowed);
    var listener = new TestSignalListener(behavior);
    inactive.SetYielding(false);
    dead.SetComponent(new LivingNaturalResource());
    dead.GetComponent<LivingNaturalResource>().Die();
    collector.Disallow(disallowed.YielderSpec);
    range.SetRange(
        readyYielder.Coordinates, inactiveYielder.Coordinates,
        deadYielder.Coordinates, disallowedYielder.Coordinates);
    blockService.SetObjectsAt(readyYielder.Coordinates, readyYielder);
    blockService.SetObjectsAt(inactiveYielder.Coordinates, inactiveYielder);
    blockService.SetObjectsAt(deadYielder.Coordinates, deadYielder);
    blockService.SetObjectsAt(disallowedYielder.Coordinates, disallowedYielder);

    component.RegisterSignalChangeCallback(Signal("Collectable.Ready", behavior), listener);
    behavior.OnEnterFinishedState();

    Assert.Equal(1, component.GetSignalSource("Collectable.Ready", behavior)().AsInt);
    Assert.Equal(1, listener.Calls);
    Assert.Equal("Collectable.Ready", listener.LastSignalName);

    ready.SetYielding(false);
    ready.DecreaseYield();
    MonoBehaviour.RunQueuedCoroutines();

    Assert.Equal(0, component.GetSignalSource("Collectable.Ready", behavior)().AsInt);
    Assert.Equal(2, listener.Calls);
  }

  public static void UpdatesReadyYieldersOnPostLoadActivation() {
    var blockService = new BlockService();
    var component = CreateComponent(blockService);
    var behavior = CreateBehavior(blockService);
    var range = behavior.GetComponent<BuildingTerrainRange>();
    var readyYielder = CreateYielderBlock(new Vector3Int(1, 2, 0), out _);
    var listener = new TestSignalListener(behavior);

    try {
      SetAutomationSystemReady(false);

      component.RegisterSignalChangeCallback(Signal("Collectable.Ready", behavior), listener);
      behavior.OnEnterFinishedState();
      range.SetRange(readyYielder.Coordinates);
      blockService.SetObjectsAt(readyYielder.Coordinates, readyYielder);
      range.RaiseRangeChanged();
      MonoBehaviour.RunQueuedCoroutines();

      Assert.Equal(0, component.GetSignalSource("Collectable.Ready", behavior)().AsInt);
      Assert.Equal(0, listener.Calls);

      behavior.ActivateDynamicComponents();

      Assert.Equal(1, component.GetSignalSource("Collectable.Ready", behavior)().AsInt);
      Assert.Equal(1, listener.Calls);
    } finally {
      SetAutomationSystemReady(true);
      MonoBehaviour.ClearQueuedCoroutines();
    }
  }

  public static void IgnoresYieldersOutsideLumberjackCuttingArea() {
    var blockService = new BlockService();
    var treeCuttingArea = new TreeCuttingArea();
    var component = CreateComponent(blockService, treeCuttingArea);
    var behavior = CreateBehavior(blockService, treeCuttingArea);
    var range = behavior.GetComponent<BuildingTerrainRange>();
    var yielderBlock = CreateYielderBlock(new Vector3Int(1, 2, 0), out _);
    behavior.SetComponent(new LumberjackFlagWorkplaceBehavior());
    range.SetRange(yielderBlock.Coordinates);
    blockService.SetObjectsAt(yielderBlock.Coordinates, yielderBlock);
    treeCuttingArea.SetInCuttingArea(yielderBlock.Coordinates, inArea: false);

    var source = component.GetSignalSource("Collectable.Ready", behavior);
    behavior.OnEnterFinishedState();

    Assert.Equal(0, source().AsInt);
  }

  static CollectableScriptableComponent CreateComponent(
      BlockService blockService = null, TreeCuttingArea treeCuttingArea = null) {
    SetDependencyContainer(CreateContainer(blockService, treeCuttingArea));
    var component = new CollectableScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static AutomationBehavior CreateBehavior(BlockService blockService, TreeCuttingArea treeCuttingArea = null) {
    SetDependencyContainer(CreateContainer(blockService, treeCuttingArea));
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new BlockObject());
    behavior.SetComponent(new YieldRemovingBuilding());
    behavior.SetComponent(new BuildingTerrainRange());
    behavior.InjectDependencies(new AutomationService());
    behavior.Awake();
    return behavior;
  }

  static TestContainer CreateContainer(BlockService blockService, TreeCuttingArea treeCuttingArea) {
    var container = new TestContainer();
    container.Register(() => new EventBus());
    container.Register(() => blockService ?? new BlockService());
    container.Register(() => treeCuttingArea ?? new TreeCuttingArea());
    return container;
  }

  static BlockObject CreateYielderBlock(Vector3Int coordinates, out Yielder yielder) {
    yielder = new Yielder();
    var blockObject = new BlockObject { Coordinates = coordinates };
    blockObject.SetComponent(yielder);
    return blockObject;
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

  static void SetAutomationSystemReady(bool isReady) {
    var setter = typeof(AutomationService)
        .GetProperty(nameof(AutomationService.AutomationSystemReady))
        ?.GetSetMethod(nonPublic: true);
    setter.Invoke(null, [isReady]);
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
}

using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockSystem;
using Timberborn.Explosions;
using Timberborn.Localization;
using Timberborn.PrioritySystem;
using UnityEngine;

namespace Automation.Tests;

static class DynamiteScriptableComponentTests {
  public static void ExposesActionsForDynamite() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());

    var actionNames = component.GetActionNamesForBuilding(behavior);

    Assert.Equal("Dynamite.Detonate", actionNames[0]);
    Assert.Equal("Dynamite.DetonateAndRepeat", actionNames[1]);
  }

  public static void HidesActionsForMissingDynamite() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();

    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
  }

  public static void BuildsActionDefinitions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();

    var detonateDef = component.GetActionDefinition("Dynamite.Detonate", behavior);
    var repeatDef = component.GetActionDefinition("Dynamite.DetonateAndRepeat", behavior);

    Assert.Equal("Dynamite.Detonate", detonateDef.ScriptName);
    Assert.Equal(0, detonateDef.Arguments.Length);
    Assert.Equal("Dynamite.DetonateAndRepeat", repeatDef.ScriptName);
    Assert.Equal(ScriptValue.TypeEnum.Number, repeatDef.Arguments[0].ValueType);
    Assert.Equal((1, 6), repeatDef.Arguments[0].DisplayNumericFormatRange);
  }

  public static void ValidatesRepeatCount() {
    var component = CreateComponent();
    var repeatDef = component.GetActionDefinition("Dynamite.DetonateAndRepeat", new AutomationBehavior());

    repeatDef.Arguments[0].ArgumentValidator(ConstantValueExpr.CreateFromValue(ScriptValue.FromInt(2)));

    Assert.Throws<ScriptError.ParsingError>(
        () => repeatDef.Arguments[0].ArgumentValidator(new TestNonConstantValueExpr()));
    Assert.Throws<ScriptError.ParsingError>(
        () => repeatDef.Arguments[0].ArgumentValidator(
            ConstantValueExpr.CreateFromValue(ScriptValue.FromString("2"))));
  }

  public static void ExecutesDetonateActionByQueueingCoroutine() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());
    var action = Action("Dynamite.Detonate", behavior);
    var beforeCount = MonoBehaviour.QueuedCoroutineCount;

    component.InstallAction(action, behavior);
    action.Execute();

    Assert.Equal(beforeCount + 1, MonoBehaviour.QueuedCoroutineCount);
    MonoBehaviour.ClearQueuedCoroutines();
  }

  public static void WaitAndPlaceRepeatsDynamiteInOrder() {
    var rule = new DynamiteScriptableComponent.DetonateAndMaybeRepeatRule();
    var repeatService = new TestRepeatService();
    rule.RepeatService = repeatService;

    RunCoroutine(rule.WaitAndPlace(new AutomationBehavior(), Priority.High, 2));

    Assert.Equal(
        "CaptureTarget,GetEffectiveDepth,IsDynamiteAlive,IsOccupantPresent,IsDynamiteAlive,Detonate,"
        + "IsDynamiteAlive,GetExpectedPlaceCoordinates,CanPlaceAt,TryPlaceDynamite,GetPlacedDynamite,"
        + "ConfigurePlacedDynamite",
        string.Join(",", repeatService.Calls));
    Assert.Equal(Priority.High, repeatService.BuilderPriority);
    Assert.Equal(2, repeatService.RepeatCount);
  }

  public static void InstallsAndUninstallsActions() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());
    var action = Action("Dynamite.Detonate", behavior);

    component.InstallAction(action, behavior);
    Assert.True(behavior.TryGetDynamicComponent<DynamiteScriptableComponent.DynamiteStateController>(out _));

    component.UninstallAction(action, behavior);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Dynamite());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionExecutor("Dynamite.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Dynamite.Missing", behavior));
  }

  static DynamiteScriptableComponent CreateComponent() {
    SetDependencyContainer(new TestContainer());
    var component = new DynamiteScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static ActionOperator Action(string actionName, AutomationBehavior behavior) {
    return ActionOperator.Create(new ExpressionContext { ScriptHost = behavior }, actionName, []);
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  static void RunCoroutine(System.Collections.IEnumerator enumerator) {
    var steps = 0;
    while (enumerator.MoveNext()) {
      if (++steps > 20) {
        throw new System.InvalidOperationException("Coroutine did not finish.");
      }
    }
  }

  sealed class TestNonConstantValueExpr : IValueExpr {
    public ScriptValue.TypeEnum ValueType => ScriptValue.TypeEnum.Number;
    public System.Func<ScriptValue> ValueFn => () => ScriptValue.FromInt(2);

    public void VisitNodes(System.Action<IExpression> visitorFn) {
      visitorFn(this);
    }
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }

  sealed class TestRepeatService : DynamiteScriptableComponent.DynamiteRepeatService {
    readonly DynamiteScriptableComponent.DynamiteRepeatTarget _target = new(
        "Dynamite", new Dynamite(), new BlockObject(), new Vector3Int(4, 5, 6));
    bool _alive = true;

    public System.Collections.Generic.List<string> Calls { get; } = [];
    public Priority BuilderPriority { get; private set; }
    public int RepeatCount { get; private set; }

    public override DynamiteScriptableComponent.DynamiteRepeatTarget CaptureTarget(AutomationBehavior behavior) {
      Calls.Add(nameof(CaptureTarget));
      return _target;
    }

    public override int GetEffectiveDepth(DynamiteScriptableComponent.DynamiteRepeatTarget target) {
      Calls.Add(nameof(GetEffectiveDepth));
      return 2;
    }

    public override bool IsDynamiteAlive(DynamiteScriptableComponent.DynamiteRepeatTarget target) {
      Calls.Add(nameof(IsDynamiteAlive));
      return _alive;
    }

    public override bool IsOccupantPresent(DynamiteScriptableComponent.DynamiteRepeatTarget target) {
      Calls.Add(nameof(IsOccupantPresent));
      return false;
    }

    public override void Detonate(DynamiteScriptableComponent.DynamiteRepeatTarget target) {
      Calls.Add(nameof(Detonate));
      _alive = false;
    }

    public override Vector3Int GetExpectedPlaceCoordinates(
        DynamiteScriptableComponent.DynamiteRepeatTarget target, int effectiveDepth) {
      Calls.Add(nameof(GetExpectedPlaceCoordinates));
      return new Vector3Int(4, 5, 4);
    }

    public override bool CanPlaceAt(Vector3Int expectedPlaceCoord) {
      Calls.Add(nameof(CanPlaceAt));
      return true;
    }

    public override bool TryPlaceDynamite(string blueprintName, Vector3Int expectedPlaceCoord) {
      Calls.Add(nameof(TryPlaceDynamite));
      return true;
    }

    public override BlockObject GetPlacedDynamite(Vector3Int expectedPlaceCoord) {
      Calls.Add(nameof(GetPlacedDynamite));
      return new BlockObject();
    }

    public override void ConfigurePlacedDynamite(BlockObject newDynamite, Priority builderPriority, int repeatCount) {
      Calls.Add(nameof(ConfigurePlacedDynamite));
      BuilderPriority = builderPriority;
      RepeatCount = repeatCount;
    }
  }
}

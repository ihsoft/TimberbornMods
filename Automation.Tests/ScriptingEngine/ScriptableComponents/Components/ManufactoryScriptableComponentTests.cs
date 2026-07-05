using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.Workshops;

namespace Automation.Tests;

static class ManufactoryScriptableComponentTests {
  public static void ExposesRecipeSignalsForManufactory() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    behavior.SetComponent(CreateManufactory());

    var signalNames = component.GetSignalNamesForBuilding(behavior);
    var recipeSignalDef = component.GetSignalDefinition("Manufactory.Recipe", behavior);
    var finishedRecipeSignalDef = component.GetSignalDefinition("Manufactory.FinishedRecipe", behavior);

    Assert.Equal("Manufactory.Recipe", signalNames[0]);
    Assert.Equal("Manufactory.FinishedRecipe", signalNames[1]);
    Assert.Equal("first", recipeSignalDef.Result.Options[0].Value);
    Assert.True(recipeSignalDef.Result.AllowCustomOptions);
    Assert.Equal(ScriptValue.TypeEnum.Number, finishedRecipeSignalDef.Result.ValueType);
    Assert.Equal(ValueDef.NumericFormatEnum.Integer, finishedRecipeSignalDef.Result.DisplayNumericFormat);
  }

  public static void ReportsCurrentRecipeSignalIncludingDynamicRecipe() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    var manufactory = CreateManufactory();
    behavior.SetComponent(manufactory);

    Assert.Equal("first", component.GetSignalSource("Manufactory.Recipe", behavior)().AsString);

    manufactory.CurrentRecipe = new RecipeSpec { Id = "dynamic", DisplayLocKey = "loc.dynamic" };

    Assert.Equal("dynamic", component.GetSignalSource("Manufactory.Recipe", behavior)().AsString);
  }

  public static void ReportsZeroFinishedRecipeSignalBeforeProductionFinishes() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    behavior.SetComponent(CreateManufactory());

    Assert.Equal(0, component.GetSignalSource("Manufactory.FinishedRecipe", behavior)().AsInt);
  }

  public static void ExposesActionForManufactoryWithMultipleRecipes() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    behavior.SetComponent(CreateManufactory());

    Assert.Equal("Manufactory.SetRecipe", component.GetActionNamesForBuilding(behavior)[0]);
  }

  public static void HidesActionForMissingOrSingleRecipeManufactory() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new Manufactory { ProductionRecipes = [new RecipeSpec { Id = "one" }] });

    Assert.Equal(0, component.GetActionNamesForBuilding(new AutomationBehavior()).Length);
    Assert.Equal(0, component.GetActionNamesForBuilding(behavior).Length);
  }

  public static void BuildsRecipeActionDefinition() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    behavior.SetComponent(CreateManufactory());

    var actionDef = component.GetActionDefinition("Manufactory.SetRecipe", behavior);

    Assert.Equal("Manufactory.SetRecipe", actionDef.ScriptName);
    Assert.Equal(1, actionDef.Arguments.Length);
    Assert.Equal("first", actionDef.Arguments[0].Options[0].Value);
    Assert.Equal("loc.first", actionDef.Arguments[0].Options[0].Text);
    Assert.True(actionDef.Arguments[0].AllowCustomOptions);
  }

  public static void ExecutesSetRecipeAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    var manufactory = CreateManufactory();
    behavior.SetComponent(manufactory);

    component.GetActionExecutor("Manufactory.SetRecipe", behavior)([ScriptValue.FromString("second")]);

    Assert.Equal("second", manufactory.CurrentRecipe.Id);
    Assert.Equal(1, manufactory.SetRecipeCalls);

    component.GetActionExecutor("Manufactory.SetRecipe", behavior)([ScriptValue.FromString("second")]);

    Assert.Equal(1, manufactory.SetRecipeCalls);
  }

  public static void ReportsUnknownRecipeAndAction() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    behavior.SetComponent(CreateManufactory());

    Assert.Throws<ScriptError.BadValue>(
        () => component.GetActionExecutor("Manufactory.SetRecipe", behavior)([ScriptValue.FromString("missing")]));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Manufactory.Missing", behavior));
  }

  public static void RecipeTrackerCountsFinishedRecipeIterationsAndResetsOnRecipeChange() {
    var component = CreateComponent();
    var behavior = new AutomationBehavior { Name = "Manufactory" };
    var manufactory = CreateManufactory();
    behavior.SetComponent(manufactory);
    component.Load();
    var tracker = new ManufactoryScriptableComponent.ManufactoryRecipeTracker();
    tracker.Initialize(behavior);
    tracker.Awake();
    var recipeListener = new TestSignalListener(behavior);
    var finishedListener = new TestSignalListener(behavior);
    tracker.AddSignal(Signal("Manufactory.Recipe", behavior, () => ScriptValue.FromString(manufactory.CurrentRecipe.Id)),
                       recipeListener);
    tracker.AddSignal(Signal("Manufactory.FinishedRecipe", behavior, tracker.FinishedRecipeSignal), finishedListener);

    manufactory.FinishProduction();
    manufactory.FinishProduction();
    manufactory.SetRecipe(manufactory.ProductionRecipes[1]);
    manufactory.FinishProduction();
    manufactory.FinishProduction();
    manufactory.FinishProduction();

    Assert.Equal(1, recipeListener.Calls);
    Assert.Equal(6, finishedListener.Calls);
    Assert.Equal(3, tracker.FinishedRecipeSignal().AsInt);
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior, Func<ScriptValue> source) {
    var scriptable = new TestScriptable("Manufactory");
    var valueType = signalName == "Manufactory.Recipe" ? ScriptValue.TypeEnum.String : ScriptValue.TypeEnum.Number;
    scriptable.RegisterSignal(signalName, valueType, source);
    TestScripting.CreateService(scriptable);
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  static Manufactory CreateManufactory() {
    var first = new RecipeSpec { Id = "first", DisplayLocKey = "loc.first" };
    var second = new RecipeSpec { Id = "second", DisplayLocKey = "loc.second" };
    return new Manufactory {
        ProductionRecipes = [first, second],
        CurrentRecipe = first,
    };
  }

  static ManufactoryScriptableComponent CreateComponent() {
    var component = new ManufactoryScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    return component;
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }

  sealed class TestSignalListener(AutomationBehavior behavior) : ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;
    public int Calls { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
    }
  }
}

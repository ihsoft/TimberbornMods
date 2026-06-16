using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.Localization;
using Timberborn.Workshops;

namespace Automation.Tests;

static class ManufactoryScriptableComponentTests {
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
}

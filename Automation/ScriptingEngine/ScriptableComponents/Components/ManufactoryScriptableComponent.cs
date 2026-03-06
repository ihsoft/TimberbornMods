// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ManufactoryScriptableComponent : ScriptableComponentBase {

  const string SetRecipeActionLocKey = "IgorZ.Automation.Scriptable.Manufactory.Action.SetRecipe";

  const string SetRecipeActionName = "Manufactory.SetRecipe";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Manufactory";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var manufactory = behavior.GetComponent<Manufactory>();
    if (manufactory == null) {
      return [];
    }
    return [SetRecipeActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var manufactory = behavior.GetComponent<Manufactory>();
    if (manufactory == null) {
      throw new UnknownActionException(name);
    }
    return name switch {
        SetRecipeActionName => args => SetRecipeAction(args, manufactory),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var manufactory = behavior.GetComponent<Manufactory>();
    if (manufactory == null) {
      throw new UnknownActionException(name);
    }
    var key = $"{name}-{behavior.Name}";
    return name switch {
        SetRecipeActionName => LookupActionDef(key, () => MakeSetRecipeActionDef(manufactory)),
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Actions

  ActionDef MakeSetRecipeActionDef(Manufactory manufactory) {
    var options = manufactory.ProductionRecipes
        .Select(x => new DropdownItem<string> { Value = x.Id, Text = Loc.T(x.DisplayLocKey) });
    return new ActionDef {
        ScriptName = SetRecipeActionName,
        DisplayName = Loc.T(SetRecipeActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.String,
                ValueFormatter = arg => Loc.T(GetRecipeSpecOrThrow(arg, manufactory).DisplayLocKey),
                RuntimeValueValidator = arg => GetRecipeSpecOrThrow(arg, manufactory),
                Options = options.ToArray(),
            },
        ],
    };
  }

  static void SetRecipeAction(ScriptValue[] args, Manufactory manufactory) {
    AssertActionArgsCount(SetRecipeActionName, args, 1);
    var recipeSpec = GetRecipeSpecOrThrow(args[0], manufactory);
    if (manufactory.CurrentRecipe != recipeSpec) {
      manufactory.SetRecipe(recipeSpec);
    }
  }

  static RecipeSpec GetRecipeSpecOrThrow(ScriptValue arg, Manufactory manufactory) {
    var recipeId = arg.AsString;
    return manufactory.ProductionRecipes.SingleOrDefault(x => x.Id == recipeId)
        ?? throw new ScriptError.BadValue($"Unknown recipe id: {recipeId}");
  }

  #endregion
}

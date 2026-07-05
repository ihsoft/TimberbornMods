// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using Timberborn.BaseComponentSystem;
using Timberborn.Workshops;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ManufactoryScriptableComponent : ScriptableComponentBase {

  const string SetRecipeActionLocKey = "IgorZ.Automation.Scriptable.Manufactory.Action.SetRecipe";
  const string RecipeSignalLocKey = "IgorZ.Automation.Scriptable.Manufactory.Signal.Recipe";
  const string FinishedRecipeSignalLocKey = "IgorZ.Automation.Scriptable.Manufactory.Signal.FinishedRecipe";

  const string SetRecipeActionName = "Manufactory.SetRecipe";
  const string RecipeSignalName = "Manufactory.Recipe";
  const string FinishedRecipeSignalName = "Manufactory.FinishedRecipe";

  static readonly string[] SignalNames = [
      RecipeSignalName,
      FinishedRecipeSignalName,
  ];

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Manufactory";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<Manufactory>() ? SignalNames : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var manufactory = GetComponentOrThrow<Manufactory>(behavior);
    return name switch {
        RecipeSignalName => () => RecipeSignal(manufactory),
        FinishedRecipeSignalName => () => FinishedRecipeSignal(behavior, manufactory),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    var manufactory = GetComponentOrThrow<Manufactory>(behavior);
    return name switch {
        RecipeSignalName =>
            _signalDefsCache.GetOrAdd($"{name}-{behavior.Name}", _ => MakeRecipeSignalDef(manufactory)),
        FinishedRecipeSignalName =>
            _signalDefsCache.GetOrAdd($"{name}-{behavior.Name}", _ => MakeFinishedRecipeSignalDef(manufactory)),
        _ => throw new UnknownSignalException(name),
    };
  }
  readonly ObjectsCache<SignalDef> _signalDefsCache = new();

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name is RecipeSignalName or FinishedRecipeSignalName) {
      host.Behavior.GetOrCreate<ManufactoryRecipeTracker>().AddSignal(signalOperator, host);
    } else {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    var name = signalOperator.SignalName;
    if (name is RecipeSignalName or FinishedRecipeSignalName) {
      host.Behavior.GetOrThrow<ManufactoryRecipeTracker>().RemoveSignal(signalOperator, host);
    } else {
      throw new InvalidOperationException("Unknown signal: " + name);
    }
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var manufactory = behavior.GetComponent<Manufactory>();
    if (manufactory == null || manufactory.ProductionRecipes.Length <= 1) {
      return [];
    }
    return [SetRecipeActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var manufactory = GetComponentOrThrow<Manufactory>(behavior);
    return name switch {
        SetRecipeActionName => args => SetRecipeAction(args, manufactory),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var manufactory = GetComponentOrThrow<Manufactory>(behavior);
    return name switch {
        SetRecipeActionName =>
            _actionDefsCache.GetOrAdd($"{name}-{behavior.Name}", _ => MakeSetRecipeActionDef(manufactory)),
        _ => throw new UnknownActionException(name),
    };
  }
  readonly ObjectsCache<ActionDef> _actionDefsCache = new();

  #endregion

  #region Signals

  SignalDef MakeRecipeSignalDef(Manufactory manufactory) {
    return new SignalDef {
        ScriptName = RecipeSignalName,
        DisplayName = Loc.T(RecipeSignalLocKey),
        Scope = SignalDef.ScopeEnum.Building,
        Result = MakeRecipeValueDef(manufactory),
    };
  }

  SignalDef MakeFinishedRecipeSignalDef(Manufactory manufactory) {
    return new SignalDef {
        ScriptName = FinishedRecipeSignalName,
        DisplayName = Loc.T(FinishedRecipeSignalLocKey),
        Scope = SignalDef.ScopeEnum.Building,
        Result = new ValueDef {
            ValueType = ScriptValue.TypeEnum.Number,
            DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
            DisplayNumericFormatRange = (0, float.NaN),
        },
    };
  }

  ValueDef MakeRecipeValueDef(Manufactory manufactory) {
    return new ValueDef {
        ValueType = ScriptValue.TypeEnum.String,
        Options = GetRecipeOptions(manufactory),
        AllowCustomOptions = true,
    };
  }

  static ScriptValue RecipeSignal(Manufactory manufactory) {
    return ScriptValue.FromString(manufactory.CurrentRecipe?.Id ?? "");
  }

  static ScriptValue FinishedRecipeSignal(AutomationBehavior behavior, Manufactory manufactory) {
    return behavior.TryGetDynamicComponent<ManufactoryRecipeTracker>(out var tracker)
        ? tracker.FinishedRecipeSignal()
        : ScriptValue.FromInt(0);
  }

  #endregion

  #region Actions

  ActionDef MakeSetRecipeActionDef(Manufactory manufactory) {
    return new ActionDef {
        ScriptName = SetRecipeActionName,
        DisplayName = Loc.T(SetRecipeActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.String,
                Options = GetRecipeOptions(manufactory),
                AllowCustomOptions = true,
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
    var recipe = manufactory.ProductionRecipes.SingleOrDefault(x => x.Id == recipeId);
    if (recipe != null) {
      return recipe;
    }
    var allowedIds = manufactory.ProductionRecipes.Select(x => x.Id).ToArray();
    throw new ScriptError.BadValue($"Unknown recipe id: {recipeId}. Allowed: {string.Join(", ", allowedIds)}");
  }

  #endregion

  #region Implementation

  DropdownItem[] GetRecipeOptions(Manufactory manufactory) {
    return manufactory.ProductionRecipes
        .Select(x => new DropdownItem { Value = x.Id, Text = Loc.T(x.DisplayLocKey) })
        .ToArray();
  }

  #endregion

  #region Manufactory recipe tracker

  internal sealed class ManufactoryRecipeTracker : AbstractStatusTracker, IAwakableComponent {

    public void Awake() {
      _manufactory = AutomationBehavior.GetComponentOrFail<Manufactory>();
      _manufactory.RecipeChanged += OnRecipeChanged;
      _manufactory.ProductionFinished += OnProductionFinished;
    }

    public ScriptValue FinishedRecipeSignal() {
      return ScriptValue.FromInt(_finishedCurrentRecipeCount);
    }

    void OnRecipeChanged(object sender, EventArgs e) {
      _finishedCurrentRecipeCount = 0;
      TriggerSignalUpdate(RecipeSignalName);
      TriggerSignalUpdate(FinishedRecipeSignalName);
    }

    void OnProductionFinished(object sender, EventArgs e) {
      _finishedCurrentRecipeCount++;
      TriggerSignalUpdate(FinishedRecipeSignalName);
    }

    Manufactory _manufactory;
    int _finishedCurrentRecipeCount;
  }

  #endregion
}

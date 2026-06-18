// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class FillValveScriptableComponent : ScriptableComponentBase {

  const string OpenActionLocKey = "IgorZ.Automation.Scriptable.FillValve.Action.Open";
  const string CloseActionLocKey = "IgorZ.Automation.Scriptable.FillValve.Action.Close";
  const string SetHeightActionLocKey = "IgorZ.Automation.Scriptable.FillValve.Action.SetHeight";

  const string OpenActionName = "FillValve.Open";
  const string CloseActionName = "FillValve.Close";
  const string SetHeightActionName = "FillValve.SetHeight";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "FillValve";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<FillValve>() ? [OpenActionName, CloseActionName, SetHeightActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var fillValve = GetComponentOrThrow<FillValve>(behavior);
    return name switch {
        OpenActionName => args => OpenAction(fillValve, args),
        CloseActionName => args => CloseAction(fillValve, args),
        SetHeightActionName => args => SetHeightAction(fillValve, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var fillValve = GetComponentOrThrow<FillValve>(behavior);
    return name switch {
        OpenActionName => OpenActionDef,
        CloseActionName => CloseActionDef,
        SetHeightActionName => _actionDefsCache.GetOrAdd(name, GetMaxTargetDepth(fillValve), MakeSetActionDef),
        _ => throw new UnknownActionException(name),
    };
  }
  readonly ObjectsCache<ActionDef> _actionDefsCache = new();

  #endregion

  #region Actions

  ActionDef OpenActionDef => _openActionDef ??= new ActionDef {
      ScriptName = OpenActionName,
      DisplayName = Loc.T(OpenActionLocKey),
      Arguments = [],
  };
  ActionDef _openActionDef;

  ActionDef CloseActionDef => _closeActionDef ??= new ActionDef {
      ScriptName = CloseActionName,
      DisplayName = Loc.T(CloseActionLocKey),
      Arguments = [],
  };
  ActionDef _closeActionDef;

  ActionDef MakeSetActionDef(string actionName, float maxTargetDepth) {
    return new ActionDef {
        ScriptName = SetHeightActionName,
        DisplayName = Loc.T(SetHeightActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.Number,
                DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
                DisplayNumericFormatRange = (0, maxTargetDepth),
                RuntimeValueValidator = ValueDef.RangeCheckValidator(min: 0, max: maxTargetDepth),
            },
        ],
    };
  }

  static void OpenAction(FillValve fillValve, ScriptValue[] args) {
    AssertActionArgsCount(OpenActionName, args, 0);
    SetTargetDepth(fillValve, GetMaxTargetDepth(fillValve));
  }

  static void CloseAction(FillValve fillValve, ScriptValue[] args) {
    AssertActionArgsCount(CloseActionName, args, 0);
    SetTargetDepth(fillValve, 0);
  }

  static void SetHeightAction(FillValve fillValve, ScriptValue[] args) {
    AssertActionArgsCount(SetHeightActionName, args, 1);
    SetTargetDepth(fillValve, args[0].AsFloat);
  }

  static void SetTargetDepth(FillValve fillValve, float targetDepth) {
    fillValve.SetTargetHeightEnabledAndSynchronize(true);
    fillValve.SetTargetHeightAndSynchronize(fillValve.MinTargetHeight + targetDepth);
  }

  static float GetMaxTargetDepth(FillValve fillValve) {
    return fillValve.MaxTargetHeight - fillValve.MinTargetHeight;
  }

  #endregion
}

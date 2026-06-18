// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.WaterBuildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ThrottlingValveScriptableComponent : ScriptableComponentBase {

  const string OpenActionLocKey = "IgorZ.Automation.Scriptable.ThrottlingValve.Action.Open";
  const string CloseActionLocKey = "IgorZ.Automation.Scriptable.ThrottlingValve.Action.Close";
  const string SetFlowActionLocKey = "IgorZ.Automation.Scriptable.ThrottlingValve.Action.SetFlow";

  const string OpenActionName = "ThrottlingValve.Open";
  const string CloseActionName = "ThrottlingValve.Close";
  const string SetFlowActionName = "ThrottlingValve.SetFlow";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "ThrottlingValve";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<ThrottlingValve>() ? [OpenActionName, CloseActionName, SetFlowActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var valve = GetComponentOrThrow<ThrottlingValve>(behavior);
    return name switch {
        OpenActionName => args => OpenAction(valve, args),
        CloseActionName => args => CloseAction(valve, args),
        SetFlowActionName => args => SetFlowAction(valve, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var valve = GetComponentOrThrow<ThrottlingValve>(behavior);
    return name switch {
        OpenActionName => OpenActionDef,
        CloseActionName => CloseActionDef,
        SetFlowActionName => _actionDefsCache.GetOrAdd(name, valve.MaxOutflowLimit, MakeSetFlowActionDef),
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

  ActionDef MakeSetFlowActionDef(string actionName, float maxOutflowLimit) {
    return new ActionDef {
        ScriptName = SetFlowActionName,
        DisplayName = Loc.T(SetFlowActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.Number,
                DisplayNumericFormat = ValueDef.NumericFormatEnum.Float,
                DisplayNumericFormatRange = (0, maxOutflowLimit),
                RuntimeValueValidator = ValueDef.RangeCheckValidator(min: 0, max: maxOutflowLimit),
            },
        ],
    };
  }

  static void OpenAction(ThrottlingValve valve, ScriptValue[] args) {
    AssertActionArgsCount(OpenActionName, args, 0);
    valve.SetOutflowLimitEnabledAndSynchronize(false);
    valve.SetOutflowLimitAndSynchronize(valve.MaxOutflowLimit);
  }

  static void CloseAction(ThrottlingValve valve, ScriptValue[] args) {
    AssertActionArgsCount(CloseActionName, args, 0);
    SetOutflowLimit(valve, 0);
  }

  static void SetFlowAction(ThrottlingValve valve, ScriptValue[] args) {
    AssertActionArgsCount(SetFlowActionName, args, 1);
    SetOutflowLimit(valve, args[0].AsFloat);
  }

  static void SetOutflowLimit(ThrottlingValve valve, float outflowLimit) {
    valve.SetOutflowLimitEnabledAndSynchronize(true);
    valve.SetOutflowLimitAndSynchronize(outflowLimit);
  }

  #endregion
}

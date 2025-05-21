// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.WorkSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class WorkplaceScriptableComponent : ScriptableComponentBase {

  const string RemoveWorkersActionLocKey = "IgorZ.Automation.Scriptable.Workplace.Action.RemoveWorkers";
  const string SetWorkersActionLocKey = "IgorZ.Automation.Scriptable.Workplace.Action.SetWorkers";

  const string RemoveWorkersActionName = "Workplace.RemoveWorkers";
  const string SetWorkersActionName = "Workplace.SetWorkers";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Workplace";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior, throwIfNotFound: false);
    return workplace ? [RemoveWorkersActionName, SetWorkersActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior);
    return name switch {
        RemoveWorkersActionName => _ => ResetWorkersAction(workplace),
        SetWorkersActionName => args => SetWorkersAction(workplace, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    var workplace = GetWorkplace(behavior);
    var key = name + "-" + workplace.MaxWorkers;
    return name switch {
        RemoveWorkersActionName => RemoveWorkersActionDef,
        SetWorkersActionName => LookupActionDef(key, () => MakeSetWorkersActionDef(workplace)),
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Actions

  ActionDef RemoveWorkersActionDef => _removeWorkersActionDef ??= new ActionDef {
      ScriptName = RemoveWorkersActionName,
      DisplayName = Loc.T(RemoveWorkersActionLocKey),
      Arguments = [],
  };
  ActionDef _removeWorkersActionDef;

  ActionDef MakeSetWorkersActionDef(Workplace workplace) {
    return new ActionDef {
        ScriptName = SetWorkersActionName,
        DisplayName = Loc.T(SetWorkersActionLocKey),
        Arguments = [
            new ValueDef {
                ValueType = ScriptValue.TypeEnum.Number,
                ValueFormatter = x => x.AsFloat.ToString("0"),
                ValueValidator = ValueDef.RangeCheckValidatorInt(1, workplace.MaxWorkers),
                ValueUiHint = GetArgumentMinMaxValueHint(1, workplace.MaxWorkers),
            },
        ],
    };
  }

  static void ResetWorkersAction(Workplace building) {
    building.DesiredWorkers = 0;
    building.UnassignWorkerIfOverstaffed();
  }

  static void SetWorkersAction(Workplace building, ScriptValue[] args) {
    AssertActionArgsCount(SetWorkersActionName, args, 1);
    var numWorkers = args[0].AsInt;
    if (numWorkers < 1 || numWorkers > building.MaxWorkers) {
      throw new ScriptError.RuntimeError("Number of workers out of range: " + numWorkers);
    }
    if (building.DesiredWorkers == numWorkers) {
      return;
    }
    building.DesiredWorkers = numWorkers;
    building.UnassignWorkerIfOverstaffed();
  }

  #endregion

  #region Implementation

  static Workplace GetWorkplace(BaseComponent building, bool throwIfNotFound = true) {
    var workplace = building.GetComponentFast<Workplace>();
    if (!workplace && throwIfNotFound) {
      throw new ScriptError.BadStateError(building, "Building doesn't have Workplace");
    }
    return workplace;
  }

  #endregion
}

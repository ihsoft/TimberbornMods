﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using Timberborn.Hauling;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class PrioritizableScriptableComponent : ScriptableComponentBase {

  const string SetHaulersActionLocKey = "IgorZ.Automation.Scriptable.Prioritizable.Action.SetHaulers";
  const string ResetHaulersActionLocKey = "IgorZ.Automation.Scriptable.Prioritizable.Action.ResetHaulers";

  const string SetHaulersActionName = "Prioritizable.SetHaulers";
  const string ResetHaulersActionName = "Prioritizable.ResetHaulers";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Prioritizable";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var haulPrioritizable = behavior.GetComponentFast<HaulPrioritizable>();
    return haulPrioritizable ? [SetHaulersActionName, ResetHaulersActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var haulPrioritizable = behavior.GetComponentFast<HaulPrioritizable>();
    if (!haulPrioritizable) {
      throw new ScriptError.BadStateError(behavior, "Building is not prioritizable");
    }
    return name switch {
        SetHaulersActionName => _ => haulPrioritizable.Prioritized = true,
        ResetHaulersActionName => _ => haulPrioritizable.Prioritized = false,
        _ => base.GetActionExecutor(name, behavior),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        SetHaulersActionName => SetHaulersActionDef,
        ResetHaulersActionName => ResetHaulersActionDef,
        _ => base.GetActionDefinition(name, _),
    };
  }

  #endregion

  #region Actions

  ActionDef SetHaulersActionDef => _setHaulersActionDef ??= new ActionDef {
      ScriptName = SetHaulersActionName,
      DisplayName = Loc.T(SetHaulersActionLocKey),
      Arguments = [],
  };
  ActionDef _setHaulersActionDef;

  ActionDef ResetHaulersActionDef => _resetHaulersActionDef ??= new ActionDef {
      ScriptName = ResetHaulersActionName,
      DisplayName = Loc.T(ResetHaulersActionLocKey),
      Arguments = [],
  };
  ActionDef _resetHaulersActionDef;

  #endregion
}

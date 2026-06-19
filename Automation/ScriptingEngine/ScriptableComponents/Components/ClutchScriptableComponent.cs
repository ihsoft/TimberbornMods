// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.PowerManagement;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ClutchScriptableComponent : ScriptableComponentBase {

  const string EngageActionLocKey = "IgorZ.Automation.Scriptable.Clutch.Action.Engage";
  const string DisengageActionLocKey = "IgorZ.Automation.Scriptable.Clutch.Action.Disengage";

  const string EngageActionName = "Clutch.Engage";
  const string DisengageActionName = "Clutch.Disengage";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Clutch";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<Clutch>() ? [EngageActionName, DisengageActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var clutch = GetComponentOrThrow<Clutch>(behavior);
    return name switch {
        EngageActionName => args => EngageAction(clutch, args),
        DisengageActionName => args => DisengageAction(clutch, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        EngageActionName => EngageActionDef,
        DisengageActionName => DisengageActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Actions

  ActionDef EngageActionDef => _engageActionDef ??= new ActionDef {
      ScriptName = EngageActionName,
      DisplayName = Loc.T(EngageActionLocKey),
      Arguments = [],
  };
  ActionDef _engageActionDef;

  ActionDef DisengageActionDef => _disengageActionDef ??= new ActionDef {
      ScriptName = DisengageActionName,
      DisplayName = Loc.T(DisengageActionLocKey),
      Arguments = [],
  };
  ActionDef _disengageActionDef;

  static void EngageAction(Clutch clutch, ScriptValue[] args) {
    AssertActionArgsCount(EngageActionName, args, 0);
    clutch.SetMode(ClutchMode.Engaged);
  }

  static void DisengageAction(Clutch clutch, ScriptValue[] args) {
    AssertActionArgsCount(DisengageActionName, args, 0);
    clutch.SetMode(ClutchMode.Disengaged);
  }

  #endregion
}

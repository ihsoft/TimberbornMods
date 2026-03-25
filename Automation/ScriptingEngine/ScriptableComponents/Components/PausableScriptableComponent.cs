// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.Buildings;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class PausableScriptableComponent : ScriptableComponentBase {

  const string PauseActionLocKey = "IgorZ.Automation.Scriptable.Pausable.Action.Pause";
  const string ResumeActionLocKey = "IgorZ.Automation.Scriptable.Pausable.Action.Resume";

  const string PauseActionName = "Pausable.Pause";
  const string ResumeActionName = "Pausable.Unpause";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Pausable";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    var pausable = behavior.GetComponent<PausableBuilding>();
    if (!pausable || !pausable.IsPausable()) {
      return [];
    }
    return [PauseActionName, ResumeActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var pausableBuilding = GetComponentOrThrow<PausableBuilding>(behavior);
    return name switch {
        PauseActionName => _ => PauseAction(pausableBuilding),
        ResumeActionName => _ => ResumeAction(pausableBuilding),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    GetComponentOrThrow<PausableBuilding>(behavior);  // Verify only.
    return name switch {
        PauseActionName => PauseActionDef,
        ResumeActionName => ResumeActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  #endregion

  #region Actions

  ActionDef PauseActionDef => _pauseActionDef ??= new ActionDef {
      ScriptName = PauseActionName,
      DisplayName = Loc.T(PauseActionLocKey),
      Arguments = [],
  };
  ActionDef _pauseActionDef;

  ActionDef ResumeActionDef => _resumeActionDef ??= new ActionDef {
      ScriptName = ResumeActionName,
      DisplayName = Loc.T(ResumeActionLocKey),
      Arguments = [],
  };
  ActionDef _resumeActionDef;

  static void PauseAction(PausableBuilding building) {
    if (!building.Paused) {
      building.Pause();
    }
  }

  static void ResumeAction(PausableBuilding building) {
    if (building.Paused) {
      building.Resume();
    }
  }

  #endregion
}

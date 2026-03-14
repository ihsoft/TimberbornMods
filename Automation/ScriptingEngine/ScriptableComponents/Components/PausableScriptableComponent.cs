// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
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
    var pausableBuilding = GetPausableBuilding(behavior, throwIfNotFound: false);
    return pausableBuilding ? [PauseActionName, ResumeActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var pausableBuilding = GetPausableBuilding(behavior);
    return name switch {
        PauseActionName => _ => PauseAction(pausableBuilding),
        ResumeActionName => _ => ResumeAction(pausableBuilding),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
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

  #region Implementation

  static PausableBuilding GetPausableBuilding(BaseComponent building, bool throwIfNotFound = true) {
    var pausable = building.GetComponent<PausableBuilding>();
    if (pausable && pausable.IsPausable()) {
      return pausable;
    }
    if (throwIfNotFound) {
      throw new ScriptError.BadStateError(building, "Building is not pausable");
    }
    return null;
  }

  #endregion
}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.BaseComponentSystem;
using Timberborn.BuildingsBlocking;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class PausableScriptableComponent : ScriptableComponentBase {

  const string PauseActionLocKey = "IgorZ.Automation.Scriptable.Pausable.Action.Pause";
  const string ResumeActionLocKey = "IgorZ.Automation.Scriptable.Pausable.Action.Resume";

  const string PauseActionName = "Pausable.Pause";
  const string ResumeActionName = "Pausable.Unpause";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Pausable";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent building) {
    var pausable = building.GetComponentFast<PausableBuilding>();
    if (pausable && pausable.IsPausable()) {
      return [PauseActionName, ResumeActionName];
    }
    return [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var pausableBuilding = building.GetComponentFast<PausableBuilding>();
    if (pausableBuilding == null || !pausableBuilding.IsPausable()) {
      throw new ScriptError.BadStateError(building, "Building is not pausable");
    }
    return name switch {
        PauseActionName => args => PauseAction(pausableBuilding, args),
        ResumeActionName => args => ResumeAction(pausableBuilding, args),
        _ => throw new ScriptError.ParsingError("Unknown action: " + name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, BaseComponent _) {
    return name switch {
        PauseActionName => PauseActionDef,
        ResumeActionName => ResumeActionDef,
        _ => throw new ScriptError.ParsingError("Unknown action: " + name),
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

  static void PauseAction(PausableBuilding instance, ScriptValue[] _) {
    instance.Pause();
  }

  static void ResumeAction(PausableBuilding instance, ScriptValue[] _) {
    instance.Resume();
  }

  #endregion
}

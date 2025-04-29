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
  // FIXME: this is a workaround to let the precondition check. Drop it when "has" operator is implemented.
  const string IsPausablePropertyName = "Pausable.IsPausable";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Pausable";

  /// <inheritdoc/>
  public override Func<object> GetPropertySource(string name, BaseComponent building) {
    var pausableBuilding = GetPausableBuilding(building);
    return name switch {
        IsPausablePropertyName => () => pausableBuilding.IsPausable(),
        _ => base.GetPropertySource(name, building),
    };
  }

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(BaseComponent building) {
    var pausableBuilding = GetPausableBuilding(building, throwIfNotFound: false);
    return pausableBuilding ? [PauseActionName, ResumeActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, BaseComponent building) {
    var pausableBuilding = GetPausableBuilding(building);
    return name switch {
        PauseActionName => args => PauseAction(pausableBuilding, args),
        ResumeActionName => args => ResumeAction(pausableBuilding, args),
        _ => base.GetActionExecutor(name, building),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, BaseComponent _) {
    return name switch {
        PauseActionName => PauseActionDef,
        ResumeActionName => ResumeActionDef,
        _ => base.GetActionDefinition(name, _),
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

  #region Implementation

  static PausableBuilding GetPausableBuilding(BaseComponent building, bool throwIfNotFound = true) {
    var pausable = building.GetComponentFast<PausableBuilding>();
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

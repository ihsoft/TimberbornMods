// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.WaterBuildings;
using Timberborn.WaterSourceSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class FlowControlScriptableComponent : ScriptableComponentBase {

  const string OpenActionLocKey = "IgorZ.Automation.Scriptable.FlowControl.Action.Open";
  const string CloseActionLocKey = "IgorZ.Automation.Scriptable.FlowControl.Action.Close";

  const string OpenActionName = "FlowControl.Open";
  const string CloseActionName = "FlowControl.Close";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "FlowControl";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    if (FlowControlAccessor.Get(behavior, throwIfNotFound: false) == null) {
      return [];
    }
    return [OpenActionName, CloseActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var component = FlowControlAccessor.Get(behavior);
    return name switch {
        OpenActionName => _ => OpenAction(component),
        CloseActionName => _ => CloseAction(component),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        OpenActionName => OpenActionDef,
        CloseActionName => CloseActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

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

  static void OpenAction(FlowControlAccessor component) {
    if (!component.IsOpen) {
      component.Open();
    }
  }

  static void CloseAction(FlowControlAccessor component) {
    if (component.IsOpen) {
      component.Close();
    }
  }

  #endregion

  #region Implementation

  sealed class FlowControlAccessor {
    public static FlowControlAccessor Get(AutomationBehavior behavior, bool throwIfNotFound = true) {
      var res = new FlowControlAccessor(behavior);
      if (res._sluice || res._waterSource) {
        return res;
      }
      if (throwIfNotFound) {
        throw new ScriptError.BadStateError(behavior, "No FlowControl component found.");
      }
      return null;
    }

    public bool IsOpen => _waterSource?.IsOpen ?? _sluice?.IsOpen ?? false;

    public void Open() {
      if (_sluice) {
        HostedDebugLog.Fine(_sluice, "Opening sluice");
        _sluice.Open();
      } else {
        HostedDebugLog.Fine(_waterSource, "Opening water source regulator");
        _waterSource.Open();
      }
    }

    public void Close() {
      if (_sluice) {
        HostedDebugLog.Fine(_sluice, "Closing sluice");
        _sluice.Close();
      } else {
        HostedDebugLog.Fine(_waterSource, "Closing water source regulator");
        _waterSource.Close();
      }
    }

    readonly SluiceState _sluice;
    readonly WaterSourceRegulator _waterSource;

    FlowControlAccessor(AutomationBehavior behavior) {
      _sluice = behavior.GetComponent<SluiceState>();
      _waterSource = behavior.GetComponent<WaterSourceRegulator>();
    }
  }

  #endregion
}

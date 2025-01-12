// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WaterBuildings;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

sealed class FloodgateScriptableComponent : ILoadableSingleton, IScriptable {

  const string ActionNameLocKeyPrefix = "IgorZ.Automation.Scripting.Floodgate.Action.";
  const string SetHeightActionName = "SetHeight";

  #region IScriptable implementation

  /// <inheritdoc/>
  public string Name => "Floodgate";

  /// <inheritdoc/>
  public Type InstanceType => typeof(Floodgate);

  /// <inheritdoc/>
  public ITriggerSource GetTriggerSource(string name, BaseComponent building, Action onValueChanged) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public IScriptable.TriggerDef GetTriggerDefinition(string name) {
    throw new NotImplementedException();
  }

  /// <inheritdoc/>
  public Action GetActionExecutor(string name, BaseComponent building, string[] args) {
    var floodgate = building as Floodgate;
    switch (name) {
      case SetHeightActionName:
        if (args.Length != 1) {
          throw new ScriptError($"{SetHeightActionName} action requires 1 argument");
        }
        var value = ParseFloat(args[0]);
        return () => floodgate!.SetHeight(value);
      default:
        throw new ScriptError("Unknown action: " + name);
    }
  }

  /// <inheritdoc/>
  public IScriptable.ActionDef GetActionDefinition(string name) {
    return _actions.FirstOrDefault(a => a.Name == name) ?? throw new ScriptError("Unknown action: " + name);
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {
    _actions.Add(new IScriptable.ActionDef {
        Name = SetHeightActionName,
        DisplayName = LocAction(SetHeightActionName),
        ArgumentTypes = [
            new IScriptable.ArgumentDef {
                ArgumentType = IScriptable.ArgumentDef.Type.Float,
            },
        ],
    });
  }

  #endregion

  readonly ILoc _loc;

  readonly List<IScriptable.ActionDef> _actions = [];

  FloodgateScriptableComponent(ILoc loc, ScriptingService scriptingService) {
    _loc = loc;
    scriptingService.RegisterScriptable(this);
  }

  string LocAction(string name) {
    return _loc.T(ActionNameLocKeyPrefix + name);
  }

  static float ParseFloat(string value) {
    if (!int.TryParse(value, out var result)) {
      throw new ScriptError("Failed to parse number argument: " + value);
    }
    return result / 100f;
  }
}

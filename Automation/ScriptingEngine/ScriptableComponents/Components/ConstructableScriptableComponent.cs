// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ConstructableScriptableComponent : ScriptableComponentBase {

  const string StateSignalLocKey = "IgorZ.Automation.Scriptable.Constructable.Signal.State";
  const string StateSignalFinishedLocKey = "IgorZ.Automation.Scriptable.Constructable.Signal.State.Finished";
  const string ProgressSignalLocKey = "IgorZ.Automation.Scriptable.Constructable.Signal.Progress";

  const string StateSignalName = "Constructable.OnUnfinished.State";
  const string ProgressSignalName = "Constructable.OnUnfinished.Progress";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Constructable";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return !behavior.BlockObject.IsFinished ? [StateSignalName, ProgressSignalName] : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    switch (name) {
      case StateSignalName:
        return () => StateSignal(behavior.BlockObject);
      case ProgressSignalName:
        var constructionSite = GetComponentOrThrow<ConstructionSite>(behavior);
        return () => ProgressSignal(constructionSite);
      default:
        throw new UnknownSignalException(name);
    }
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        StateSignalName => StateSignalDef,
        ProgressSignalName => ProgressSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName is not StateSignalName and not ProgressSignalName) {
      throw new InvalidOperationException("Unknown signal: " + signalOperator.SignalName);
    }
    host.Behavior.GetOrCreate<ConstructableStateTracker>().AddSignal(signalOperator, host);
  }
  
  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<ConstructableStateTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

  SignalDef StateSignalDef => _stateSignalDef ??= new SignalDef {
      ScriptName = StateSignalName,
      DisplayName = Loc.T(StateSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.String,
          Options = [
              ("finished", Loc.T(StateSignalFinishedLocKey)),
          ],
      },
  };
  SignalDef _stateSignalDef;

  SignalDef ProgressSignalDef => _progressSignalDef ??= new SignalDef {
      ScriptName = ProgressSignalName,
      DisplayName = Loc.T(ProgressSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Percent,
          DisplayNumericFormatRange = (0, 100),
          RuntimeValueValidator = ValueDef.RangeCheckValidator(min: 0f, max: 1f),
      },
  };
  SignalDef _progressSignalDef;

  static ScriptValue StateSignal(BlockObject blockObject) {
    return ScriptValue.FromString(blockObject.IsFinished ? "finished" : "");
  }

  static ScriptValue ProgressSignal(ConstructionSite constructionSite) {
    return ScriptValue.FromFloat(constructionSite.BuildTimeProgress);
  }

  #endregion

  #region Implementation

  internal sealed class ConstructableStateTracker : AbstractStatusTracker, IAwakableComponent, IFinishedStateListener {
    int _prevProgress;

    public void Awake() {
      var constructionSite = AutomationBehavior.GetComponentOrFail<ConstructionSite>();
      constructionSite.OnConstructionSiteProgressed += (_, _) => {
        var progress = Mathf.RoundToInt(constructionSite.BuildTimeProgress * 100f);
        if (progress != _prevProgress) {
          _prevProgress = progress;
          ScheduleSignal(ProgressSignalName, ignoreErrors: true);
        }
      };
    }

    /// <inheritdoc/>
    public void OnEnterFinishedState() {
      ScheduleSignal(StateSignalName, ignoreErrors: true);
    }

    /// <inheritdoc/>
    public void OnExitFinishedState() {
    }
  }

  #endregion
}

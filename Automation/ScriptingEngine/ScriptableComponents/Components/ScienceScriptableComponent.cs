// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.ScienceSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class ScienceScriptableComponent : ScriptableComponentBase {

  const string PointsSignalLocKey = "IgorZ.Automation.Scriptable.Science.Signal.Points";

  const string PointsSignalName = "Science.Points";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Science";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return [PointsSignalName];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    return name switch {
        PointsSignalName => PointsSignal,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        PointsSignalName => PointsSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName != PointsSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    var wasInactive = _referenceManager.Signals.Count == 0;
    _referenceManager.AddSignal(signalOperator, host);
    if (wasInactive) {
      _lastSciencePoints = _scienceService.SciencePoints;
      _automationService.RegisterTickable(OnTick);
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    _referenceManager.RemoveSignal(signalOperator, host);
    if (_referenceManager.Signals.Count == 0) {
      _automationService.UnregisterTickable(OnTick);
    }
  }

  #endregion

  #region Signals

  SignalDef PointsSignalDef => _pointsSignalDef ??= new SignalDef {
      ScriptName = PointsSignalName,
      DisplayName = Loc.T(PointsSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
          DisplayNumericFormatRange = (0, float.NaN),
      },
  };
  SignalDef _pointsSignalDef;

  ScriptValue PointsSignal() {
    return ScriptValue.FromInt(_scienceService.SciencePoints);
  }

  #endregion

  #region Implemenation

  readonly AutomationService _automationService;
  readonly ScienceService _scienceService;
  readonly ReferenceManager _referenceManager;

  int _lastSciencePoints;

  ScienceScriptableComponent(
      AutomationService automationService, ScienceService scienceService, ReferenceManager referenceManager) {
    _automationService = automationService;
    _scienceService = scienceService;
    _referenceManager = referenceManager;
  }

  void OnTick(int currentTick) {
    var sciencePoints = _scienceService.SciencePoints;
    if (sciencePoints == _lastSciencePoints) {
      return;
    }
    _lastSciencePoints = sciencePoints;
    _referenceManager.TriggerSignalUpdate(PointsSignalName);
  }

  #endregion
}

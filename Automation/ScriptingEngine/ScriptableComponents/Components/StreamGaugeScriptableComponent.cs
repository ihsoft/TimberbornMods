// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.TickSystem;
using Timberborn.WaterBuildings;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class StreamGaugeScriptableComponent : ScriptableComponentBase {

  const string DepthSignalLocKey = "IgorZ.Automation.Scriptable.StreamGauge.Signal.Depth";
  const string ContaminationSignalLocKey = "IgorZ.Automation.Scriptable.StreamGauge.Signal.Contamination";
  const string CurrentSignalLocKey = "IgorZ.Automation.Scriptable.StreamGauge.Signal.Current";

  const string DepthSignalName = "StreamGauge.Depth";
  const string ContaminationSignalName = "StreamGauge.Contamination";
  const string CurrentSignalName = "StreamGauge.Current";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "StreamGauge";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<StreamGauge>()
        ? [DepthSignalName, ContaminationSignalName, CurrentSignalName]
        : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var gauge = GetGauge(behavior);
    return name switch {
        DepthSignalName => () => DepthSignal(gauge),
        ContaminationSignalName => () => ContaminationSignal(gauge),
        CurrentSignalName => () => CurrentSignal(gauge),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        DepthSignalName => DepthSignalDef,
        ContaminationSignalName => ContaminationSignalDef,
        CurrentSignalName => CurrentSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrCreate<StreamGaugeTracker>().AddSignal(signalOperator, host);
    var ticker = host.Behavior.GetComponent<StreamGaugeCheckTicker>();
    if (!ticker) {
      throw new InvalidOperationException($"No ticker component found on {host.Behavior}");
    }
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    host.Behavior.GetOrThrow<StreamGaugeTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

  SignalDef DepthSignalDef => _depthSignalDef ??= new SignalDef {
      ScriptName = DepthSignalName,
      DisplayName = Loc.T(DepthSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueFormatter = x => x.AsFloat.ToString("0.00"),
          RuntimeValueValidator = ValueDef.RangeCheckValidatorFloat(min: 0f),
      },
  };
  SignalDef _depthSignalDef;

  SignalDef ContaminationSignalDef => _contaminationSignalDef ??= new SignalDef {
      ScriptName = ContaminationSignalName,
      DisplayName = Loc.T(ContaminationSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueFormatter = x => x.AsFloat.ToString("P0"),
          ValueUiHint = GetArgumentMaxValueHint(1f),
          RuntimeValueValidator = ValueDef.RangeCheckValidatorFloat(min: 0f, max: 1f),
      },
  };
  SignalDef _contaminationSignalDef;

  SignalDef CurrentSignalDef => _currentSignalDef ??= new SignalDef {
      ScriptName = CurrentSignalName,
      DisplayName = Loc.T(CurrentSignalLocKey),
      Result = new ValueDef {
          ValueType = ScriptValue.TypeEnum.Number,
          ValueFormatter = x => x.AsFloat.ToString("0.0"),
          RuntimeValueValidator = ValueDef.RangeCheckValidatorFloat(min: 0f),
      },
  };
  SignalDef _currentSignalDef;

  static ScriptValue DepthSignal(StreamGauge gauge) {
    return ScriptValue.FromFloat(gauge.WaterLevel);
  }

  static ScriptValue ContaminationSignal(StreamGauge gauge) {
    return ScriptValue.FromFloat(gauge.ContaminationLevel);
  }

  static ScriptValue CurrentSignal(StreamGauge gauge) {
    return ScriptValue.FromFloat(gauge.WaterCurrent);
  }

  #endregion

  #region Implementation

  static StreamGauge GetGauge(AutomationBehavior behavior) {
    var streamGauge = behavior.GetComponent<StreamGauge>();
    if (!streamGauge) {
      throw new ScriptError.BadStateError(behavior, "StreamGauge component not found");
    }
    return streamGauge;
  }

  #endregion

  #region Stream gauge tracker component

  internal sealed class StreamGaugeCheckTicker : TickableComponent, IAwakableComponent {
    StreamGaugeTracker _streamGaugeTracker;

    public void Awake() {
      DisableComponent();
    }

    public override void StartTickable() {
      _streamGaugeTracker = GetComponent<AutomationBehavior>().GetOrCreate<StreamGaugeTracker>();
      base.StartTickable();
    }

    public override void Tick() {
      _streamGaugeTracker.UpdateSignals();
    }
  }

  //FXIME: disable component on the last signal unregistered, enable on first.
  internal sealed class StreamGaugeTracker : AbstractStatusTracker {
    StreamGauge _streamGauge;
    int _prevWaterLevel;
    int _prevContaminationLevel;
    int _prevWaterCurrent;

    /// <inheritdoc/>
    public override void Start() {
      base.Start();
      _streamGauge = AutomationBehavior.GetComponent<StreamGauge>();
      _prevWaterLevel = Mathf.RoundToInt(_streamGauge.WaterLevel * 100f);
      _prevContaminationLevel = Mathf.RoundToInt(_streamGauge.ContaminationLevel * 100f);
      _prevWaterCurrent = Mathf.RoundToInt(_streamGauge.WaterCurrent * 100f);
      AutomationBehavior.GetComponent<StreamGaugeCheckTicker>().EnableComponent();
    }

    public void UpdateSignals() {
      if (!_streamGauge) {
        return;
      }
      var waterLevel = Mathf.RoundToInt(_streamGauge.WaterLevel * 100f);
      if (_prevWaterLevel != waterLevel) {
        _prevWaterLevel = waterLevel;
        ScheduleSignal(DepthSignalName, ignoreErrors: true);
      }
      var contaminationLevel = Mathf.RoundToInt(_streamGauge.ContaminationLevel * 100f);
      if (_prevContaminationLevel != contaminationLevel) {
        _prevContaminationLevel = contaminationLevel;
        ScheduleSignal(ContaminationSignalName, ignoreErrors: true);
      }
      var waterCurrent = Mathf.RoundToInt(_streamGauge.WaterCurrent * 100f);
      if (_prevWaterCurrent != waterCurrent) {
        _prevWaterCurrent = waterCurrent;
        ScheduleSignal(CurrentSignalName, ignoreErrors: true);
      }
    }
  }

  #endregion
}

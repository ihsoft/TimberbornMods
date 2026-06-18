// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlockingSystem;
using Timberborn.Reproduction;
using Timberborn.TimeSystem;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class BreedingPodScriptableComponent : ScriptableComponentBase {

  const string ProgressSignalLocKey = "IgorZ.Automation.Scriptable.BreedingPod.Signal.Progress";

  const string ProgressSignalName = "BreedingPod.Progress";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "BreedingPod";

  /// <inheritdoc/>
  public override string[] GetSignalNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<BreedingPod>() ? [ProgressSignalName] : [];
  }

  /// <inheritdoc/>
  public override Func<ScriptValue> GetSignalSource(string name, AutomationBehavior behavior) {
    var breedingPod = GetComponentOrThrow<BreedingPod>(behavior);
    return name switch {
        ProgressSignalName => () => ProgressSignal(behavior, breedingPod),
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override SignalDef GetSignalDefinition(string name, AutomationBehavior behavior) {
    GetComponentOrThrow<BreedingPod>(behavior);  // Verify only.
    return name switch {
        ProgressSignalName => ProgressSignalDef,
        _ => throw new UnknownSignalException(name),
    };
  }

  /// <inheritdoc/>
  public override void RegisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName is not ProgressSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    host.Behavior.GetOrCreate<BreedingPodProgressTracker>().AddSignal(signalOperator, host);
  }

  /// <inheritdoc/>
  public override void UnregisterSignalChangeCallback(SignalOperator signalOperator, ISignalListener host) {
    if (signalOperator.SignalName is not ProgressSignalName) {
      throw new InvalidOperationException($"Unknown signal: {signalOperator.SignalName}");
    }
    host.Behavior.GetOrThrow<BreedingPodProgressTracker>().RemoveSignal(signalOperator, host);
  }

  #endregion

  #region Signals

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

  static ScriptValue ProgressSignal(AutomationBehavior behavior, BreedingPod breedingPod) {
    return behavior.TryGetDynamicComponent<BreedingPodProgressTracker>(out var tracker)
        ? tracker.ProgressSignal()
        : ProgressSignal(breedingPod);
  }

  static ScriptValue ProgressSignal(BreedingPod breedingPod) {
    return ScriptValue.FromFloat(NormalizeProgress(breedingPod.CalculateProgress()));
  }

  static float NormalizeProgress(float progress) {
    return Mathf.Clamp(progress, 0f, MaxApproximateProgress);
  }

  const float MaxApproximateProgress = 0.99f;

  #endregion

  #region Tracker for the breeding pod progress

  internal sealed class BreedingPodProgressTracker : AbstractStatusTracker, IAwakableComponent, IFinishedStateListener {

    const float ProgressSignalStep = 0.01f;
    const float MinCheckDelayInDays = 0.001f;

    #region IAwakableComponent implementation

    public void Awake() {
      _breedingPod = AutomationBehavior.GetComponentOrFail<BreedingPod>();
      _breedingPodSpec = AutomationBehavior.GetComponent<BreedingPodSpec>()
          ?? throw new InvalidOperationException("Building has no 'BreedingPodSpec'");
      _blockableObject = AutomationBehavior.GetComponentOrFail<BlockableObject>();
    }

    #endregion

    #region IFinishedStateListener implementation

    /// <inheritdoc/>
    public void OnEnterFinishedState() {
      if (_registeredSignalsCount > 0) {
        StartTracking();
      }
    }

    /// <inheritdoc/>
    public void OnExitFinishedState() {
      StopTracking();
      StopProgressCheck();
    }

    #endregion

    #region AbstractStatusTracker overrides

    /// <inheritdoc/>
    public override bool AddSignal(SignalOperator signalOperator, ISignalListener host) {
      var isFirstSignal = base.AddSignal(signalOperator, host);
      _registeredSignalsCount++;
      StartTracking();
      return isFirstSignal;
    }

    /// <inheritdoc/>
    public override bool RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
      var hasMoreListeners = base.RemoveSignal(signalOperator, host);
      _registeredSignalsCount--;
      if (_registeredSignalsCount == 0) {
        StopTracking();
        StopProgressCheck();
      }
      return hasMoreListeners;
    }

    #endregion

    #region API

    public ScriptValue ProgressSignal() {
      return _forcedProgress.HasValue
          ? ScriptValue.FromFloat(_forcedProgress.Value)
          : BreedingPodScriptableComponent.ProgressSignal(_breedingPod);
    }

    public void OnGrowthCycleFinished() {
      ForceProgressSignal(1f);
      SyncProgress();
    }

    public void OnInventoryChanged() {
      SyncProgress();
    }

    #endregion

    #region Implementation

    BreedingPod _breedingPod;
    BreedingPodSpec _breedingPodSpec;
    BlockableObject _blockableObject;
    ITimeTrigger _progressCheck;
    ITimeTriggerFactory _timeTriggerFactory;
    bool _tracking;
    int _progressCheckVersion;
    int _registeredSignalsCount;
    float? _forcedProgress;

    [Inject]
    public void InjectDependencies(ITimeTriggerFactory timeTriggerFactory) {
      _timeTriggerFactory = timeTriggerFactory;
    }

    void StartTracking() {
      if (_tracking || !AutomationBehavior.BlockObject.IsFinished) {
        return;
      }
      _tracking = true;
      _blockableObject.ObjectBlocked += OnObjectBlocked;
      _blockableObject.ObjectUnblocked += OnObjectUnblocked;
      SyncProgress();
    }

    void StopTracking() {
      if (!_tracking) {
        return;
      }
      _tracking = false;
      _blockableObject.ObjectBlocked -= OnObjectBlocked;
      _blockableObject.ObjectUnblocked -= OnObjectUnblocked;
    }

    void SyncProgress() {
      TriggerSignalUpdate(ProgressSignalName);
      ScheduleProgressCheck();
    }

    void ForceProgressSignal(float progress) {
      _forcedProgress = progress;
      TriggerSignalUpdate(ProgressSignalName);
      _forcedProgress = null;
    }

    void ScheduleProgressCheck() {
      StopProgressCheck();
      if (!_tracking || !_blockableObject.IsUnblocked || _breedingPod.ProgressHalted) {
        return;
      }
      var currentProgress = _breedingPod.CalculateProgress();
      if (currentProgress >= MaxApproximateProgress) {
        return;
      }
      var nextProgress = Mathf.Min(NextSignalThreshold(currentProgress), MaxApproximateProgress);
      var delayInDays = Mathf.Max(
          (nextProgress - currentProgress) * _breedingPodSpec.CycleLengthInDays * _breedingPod.CyclesUntilFullyGrown,
          MinCheckDelayInDays);
      var progressCheckVersion = ++_progressCheckVersion;
      _progressCheck = _timeTriggerFactory.Create(() => OnProgressCheckDue(progressCheckVersion), delayInDays);
      _progressCheck.Resume();
    }

    void StopProgressCheck() {
      _progressCheckVersion++;
      _progressCheck?.Pause();
      _progressCheck = null;
    }

    void OnProgressCheckDue(int progressCheckVersion) {
      if (progressCheckVersion != _progressCheckVersion) {
        return;
      }
      _progressCheck = null;
      SyncProgress();
    }

    static float NextSignalThreshold(float currentProgress) {
      return (Mathf.Floor(currentProgress / ProgressSignalStep) + 1f) * ProgressSignalStep;
    }

    void OnObjectBlocked(object sender, EventArgs e) {
      StopProgressCheck();
      SyncProgress();
    }

    void OnObjectUnblocked(object sender, EventArgs e) {
      SyncProgress();
    }

    #endregion
  }

  #endregion
}

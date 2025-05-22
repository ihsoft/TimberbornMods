// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

/// <summary>A base component to track the signals and actions on an automation behavior.</summary>
abstract class AbstractStatusTracker : BaseComponent {

  #region API

  /// <summary>Reference manager for signals and actions.</summary>
  public readonly ReferenceManager ReferenceManager = new();

  /// <summary>True if the component has any signals or actions.</summary>
  public bool HasSignals => ReferenceManager.Signals.Count > 0;

  /// <summary>True if the component has any actions.</summary>
  public bool HasActions => ReferenceManager.Actions.Count > 0;

  /// <inheritdoc cref="ScriptableComponents.ReferenceManager.AddSignal" />
  public void AddSignal(SignalOperator signalOperator, ISignalListener host) {
    ReferenceManager.AddSignal(signalOperator, host);
    if (ReferenceManager.Signals.Count == 1 && !HasActions) {
      OnFirstReference();
    }
  }

  /// <inheritdoc cref="ScriptableComponents.ReferenceManager.RemoveSignal" />
  public void RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
    ReferenceManager.RemoveSignal(signalOperator, host);
    if (!HasActions && !HasSignals) {
      OnLastReference();
    }
  }

  /// <inheritdoc cref="ScriptableComponents.ReferenceManager.AddAction" />
  public void AddAction(ActionOperator actionOperator) {
    ReferenceManager.AddAction(actionOperator);
    if (ReferenceManager.Actions.Count == 1 && !HasSignals) {
      OnFirstReference();
    }
  }

  /// <inheritdoc cref="ScriptableComponents.ReferenceManager.RemoveAction" />
  public void RemoveAction(ActionOperator actionOperator) {
    ReferenceManager.RemoveAction(actionOperator);
    if (!HasActions && !HasSignals) {
      OnLastReference();
    }
  }

  /// <inheritdoc cref="ScriptableComponents.ReferenceManager.ScheduleSignal" />
  public void ScheduleSignal(string signalName, bool ignoreErrors = false) =>
      ReferenceManager.ScheduleSignal(signalName, _scriptingService, ignoreErrors);

  #endregion

  #region Overrides

  /// <summary>Called when the first signal or action is registered.</summary>
  protected virtual void OnFirstReference() {}

  /// <summary>Called when the last signal or action is unregistered.</summary>
  protected virtual void OnLastReference() {}

  #endregion

  #region Imlementation

  ScriptingService _scriptingService;

  [Inject]
  public void InjectDependencies(ScriptingService scriptingService) {
    _scriptingService = scriptingService;
  }

  #endregion
}

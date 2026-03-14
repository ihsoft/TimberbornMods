// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Expressions;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

/// <summary>A base component to track the signals and actions on an automation behavior.</summary>
abstract class AbstractStatusTracker : AbstractDynamicComponent {

  #region API

  /// <summary>Reference manager for signals and actions.</summary>
  public ReferenceManager ReferenceManager { get; private set; }

  /// <summary>True if the component has any signals or actions.</summary>
  public bool HasSignals => ReferenceManager.Signals.Count > 0;

  /// <summary>True if the component has any actions.</summary>
  public bool HasActions => ReferenceManager.Actions.Count > 0;

  /// <inheritdoc cref="Components.ReferenceManager.AddSignal" />
  public virtual void AddSignal(SignalOperator signalOperator, ISignalListener host) {
    ReferenceManager.AddSignal(signalOperator, host);
    if (ReferenceManager.Signals.Count == 1 && !HasActions) {
      OnFirstReference();
    }
  }

  /// <inheritdoc cref="Components.ReferenceManager.RemoveSignal" />
  public virtual void RemoveSignal(SignalOperator signalOperator, ISignalListener host) {
    ReferenceManager.RemoveSignal(signalOperator, host);
    if (!HasActions && !HasSignals) {
      OnLastReference();
    }
  }

  /// <inheritdoc cref="Components.ReferenceManager.AddAction" />
  public virtual void AddAction(ActionOperator actionOperator) {
    ReferenceManager.AddAction(actionOperator);
    if (ReferenceManager.Actions.Count == 1 && !HasSignals) {
      OnFirstReference();
    }
  }

  /// <inheritdoc cref="Components.ReferenceManager.RemoveAction" />
  public virtual void RemoveAction(ActionOperator actionOperator) {
    ReferenceManager.RemoveAction(actionOperator);
    if (!HasActions && !HasSignals) {
      OnLastReference();
    }
  }

  /// <inheritdoc cref="Components.ReferenceManager.ScheduleSignal" />
  public void ScheduleSignal(string signalName, bool ignoreErrors = false) =>
      ReferenceManager.ScheduleSignal(signalName, ignoreErrors);

  #endregion

  #region Overrides

  /// <summary>Called when the first signal or action is registered.</summary>
  protected virtual void OnFirstReference() {}

  /// <summary>Called when the last signal or action is unregistered.</summary>
  protected virtual void OnLastReference() {}

  #endregion

  #region Imlementation

  [Inject]
  public void InjectDependencies(ReferenceManager referenceManager) {
    ReferenceManager = referenceManager;
  }

  #endregion
}

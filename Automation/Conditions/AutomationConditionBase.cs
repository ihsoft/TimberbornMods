// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics.CodeAnalysis;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Utils;
using IgorZ.TimberDev.Utils;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Conditions;

/// <summary>The base class of any automation condition.</summary>
/// <remarks>
/// The descendants of this class must encapsulate all settings of the condition and provide functionality to set up the
/// dynamic logic.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
//FIXME: Implement IClonable instead of custom clone method.
public abstract class AutomationConditionBase : IAutomationCondition {

  /// <summary>Serializer that handles persistence of all the condition types.</summary>
  /// <remarks>Loading will fail if the condition can't be loaded.</remarks>
  public static readonly DynamicClassSerializer<AutomationConditionBase> ConditionSerializer = new();

  /// <summary>Serializer that handles persistence of all the condition types.</summary>
  /// <remarks>This version returns <c>null</c> if the condition can't be loaded.</remarks>
  public static readonly DynamicClassSerializer<AutomationConditionBase> ConditionSerializerNullable = new(false);

  #region ICondition implementation

  /// <inheritdoc/>
  public virtual AutomationBehavior Behavior {
    get => _behavior;
    set {
      if (value == _behavior) {
        return;
      }
      if (_behavior) {
        OnBehaviorToBeCleared();
      }
      _behavior = value;
      if (_behavior) {
        OnBehaviorAssigned();
      }
    }
  }
  AutomationBehavior _behavior;

  /// <inheritdoc/>
  public virtual bool CanRunOnUnfinishedBuildings => false;

  /// <inheritdoc/>
  public virtual IAutomationConditionListener Listener { get; set; }

  /// <inheritdoc/>
  public virtual bool ConditionState {
    get => _conditionState;
    internal set {
      _conditionState = value;
      if (!IsActive) {
        throw new InvalidOperationException("Condition state can only be set on an activated condition.");
      }
      if (!IsMarkedForCleanup) {
        Listener.OnConditionState(this);
      }
    }
  }
  bool _conditionState;

  /// <inheritdoc/>
  public bool IsMarkedForCleanup { get; private set; }

  /// <inheritdoc/>
  public void MarkForCleanup() {
    if (IsMarkedForCleanup) {
      return;
    }
    IsMarkedForCleanup = true;
    HostedDebugLog.Fine(Behavior, "Action marked for cleanup: {0}", this);
    Behavior.AutomationService.MarkBehaviourForCleanup(Behavior);
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<bool> ConditionStateKey = new("ConditionState");
  static readonly PropertyKey<bool> IsEnabledKey = new("IsEnabled");

  /// <inheritdoc/>
  public virtual void LoadFrom(IObjectLoader objectLoader) {
    _conditionState = objectLoader.GetValueOrDefault(ConditionStateKey);
    IsEnabled = objectLoader.GetValueOrDefault(IsEnabledKey, true);
  }

  /// <inheritdoc/>
  public virtual void SaveTo(IObjectSaver objectSaver) {
    objectSaver.Set(ConditionStateKey, ConditionState);
    objectSaver.Set(IsEnabledKey, IsEnabled);
  }

  #endregion

  #region API

  /// <inheritdoc/>
  public abstract string UiDescription { get; }

  /// <inheritdoc/>
  public bool IsActive { get; private set; }

  /// <inheritdoc/>
  public bool IsEnabled { get; private set; } = true;

  /// <inheritdoc/>
  public abstract bool IsInErrorState { get; }

  /// <inheritdoc/>
  public virtual IAutomationCondition CloneDefinition() {
    var clone = (AutomationConditionBase)Activator.CreateInstance(GetType());
    clone.IsEnabled = IsEnabled;
    return clone;
  }

  /// <inheritdoc/>
  public virtual void Activate(bool noTrigger = false) {
    if (!IsEnabled) {
      throw new InvalidOperationException("Cannot activate disabled condition.");
    }
    if (IsActive) {
      throw new InvalidOperationException("Condition already activated.");
    }
    if (!Behavior || Listener == null) {
      throw new InvalidOperationException("Behavior and Listener must be set before activating the condition.");
    }
    IsActive = true;
  }

  /// <inheritdoc/>
  public void SetEnabled(bool state) {
    if (IsEnabled == state) {
      return;
    }
    if (Behavior || IsActive) {
      throw new InvalidOperationException("State must be inactive and no Behavior set to change enabled state.");
    }
    IsEnabled = state;
  }

  /// <inheritdoc/>
  public abstract bool IsValidAt(AutomationBehavior behavior);

  /// <summary>
  /// Notifies that a new behavior has been assigned to the condition. It is the time to set up the behaviors. 
  /// </summary>
  /// <seealso cref="Behavior"/>
  protected abstract void OnBehaviorAssigned();

  /// <summary>
  /// Notifies that the current behavior is about to be cleared. It is the time to clean up the behaviors. 
  /// </summary>
  /// <seealso cref="Behavior"/>
  protected abstract void OnBehaviorToBeCleared();

  #endregion

  #region Implementation

  /// <inheritdoc/>
  public override string ToString() {
    return $"TypeId={GetType()},Listener={Listener?.GetType()}";
  }

  #endregion
}
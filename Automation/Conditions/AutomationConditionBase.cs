// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Utils;
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
      if (!IsMarkedForCleanup) {
        Listener?.OnConditionState(this);
      }
    }
  }
  bool _conditionState;

  /// <inheritdoc/>
  public bool IsMarkedForCleanup {
    get => _isMarkedForCleanup;
    protected set {
      _isMarkedForCleanup = value;
      if (value) {
        HostedDebugLog.Fine(Behavior, "Action marked for cleanup: {0}", this);
        Behavior.AutomationService.MarkBehaviourForCleanup(Behavior);
      }
    }
  }
  bool _isMarkedForCleanup;

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<bool> ConditionStateKey = new("ConditionState");
  static readonly PropertyKey<bool> IsMarkedForCleanupKey = new("IsMarkedForCleanup");

  /// <inheritdoc/>
  public virtual void LoadFrom(IObjectLoader objectLoader) {
    ConditionState = objectLoader.Has(ConditionStateKey) && objectLoader.Get(ConditionStateKey);
    IsMarkedForCleanup = objectLoader.Has(IsMarkedForCleanupKey) && objectLoader.Get(IsMarkedForCleanupKey);
  }

  /// <inheritdoc/>
  public virtual void SaveTo(IObjectSaver objectSaver) {
    objectSaver.Set(ConditionStateKey, ConditionState);
    objectSaver.Set(IsMarkedForCleanupKey, IsMarkedForCleanup);
  }

  #endregion

  #region API

  /// <inheritdoc/>
  public abstract string UiDescription { get; }

  /// <inheritdoc/>
  public abstract IAutomationCondition CloneDefinition();

  /// <inheritdoc/>
  public abstract void SyncState(bool force);

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
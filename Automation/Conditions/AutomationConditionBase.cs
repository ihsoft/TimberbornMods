// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using Automation.AutomationSystem;
using Automation.Utils;
using Timberborn.Persistence;

namespace Automation.Conditions {

/// <summary>The base class of any automation condition.</summary>
/// <remarks>
/// The descendants of this class must encapsulate all settings of the condition and provide functionality to set up the
/// dynamic logic.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public abstract class AutomationConditionBase : IAutomationCondition {
  /// <summary>Serializer that handles persistence of all the condition types.</summary>
  /// <remarks>Loading will fail if the condition cannot be loaded.</remarks>
  public static readonly DynamicClassSerializer<AutomationConditionBase> ConditionSerializer = new();

  /// <summary>Serializer that handles persistence of all the condition types.</summary>
  /// <remarks>This version returns <c>null</c> if the condition cannot be loaded.</remarks>
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
  public virtual IAutomationConditionListener Listener { get; set; }

  /// <inheritdoc/>
  public virtual bool ConditionState {
    get => _conditionState;
    internal set {
      if (_conditionState != value) {
        _conditionState = value;
        Listener?.OnConditionState(this);
      }
    }
  }
  bool _conditionState;

  /// <inheritdoc/>
  /// <remarks>
  /// If custom code sets it to <c>true</c>, then it must call <see cref="AutomationBehavior.CollectCleanedRules"/> to
  /// trigger the update handling.
  /// </remarks>
  public bool IsMarkedForCleanup { get; protected set; }
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
  public abstract void SyncState();

  /// <inheritdoc/>
  public abstract bool IsValidAt(AutomationBehavior behavior);

  /// <summary>
  /// Notifies that a new behavior has been assigned to the condition. It's the time to setup the behaviors. 
  /// </summary>
  /// <seealso cref="Behavior"/>
  protected abstract void OnBehaviorAssigned();

  /// <summary>
  /// Notifies that the current behavior is about to be cleared. It's the time to cleanup the behaviors. 
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

}

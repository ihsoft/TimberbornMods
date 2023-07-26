// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using Automation.Core;
using Automation.Utils;
using Timberborn.Persistence;

namespace Automation.Conditions {

/// <summary>The base class of any automation condition.</summary>
/// <remarks>
/// <p>
/// The descendants of this class must encapsulate all settings of the condition and provide functionality to set up the
/// dynamic logic.
/// </p>
/// <p>
/// The default base implementation matches by the type name only and valid on finished block objects only. To change
/// this, override <see cref="CheckSameDefinition"/> and <see cref="IsValidAt"/>.
/// </p>
/// </remarks>
/// <seealso cref="AutomationConditionBehaviorBase"/>
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
      if (value == null || _behavior != null) {
        OnBehaviorToBeCleared();
      }
      _behavior = value;
      if (_behavior != null) {
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
  public virtual bool CheckSameDefinition(IAutomationCondition other) {
    return other != null && other.GetType() == GetType();
  }

  /// <inheritdoc/>
  public virtual bool IsValidAt(AutomationBehavior behavior) {
    return behavior.BlockObject.Finished;
  }

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

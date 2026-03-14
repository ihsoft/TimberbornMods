// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Diagnostics.CodeAnalysis;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.Utils;
using IgorZ.TimberDev.Utils;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

/// <summary>The base class for all automation actions.</summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public abstract class AutomationActionBase : IAutomationAction, IAutomationConditionListener {
  /// <summary>Serializer that handles persistence of all the action types.</summary>
  /// <remarks>Loading will fail if the action can't be loaded.</remarks>
  public static readonly DynamicClassSerializer<AutomationActionBase> ActionSerializer = new();

  /// <summary>Serializer that handles persistence of all the action types.</summary>
  /// <remarks>This version returns <c>null</c> if the action can't be loaded.</remarks>
  public static readonly DynamicClassSerializer<AutomationActionBase> ActionSerializerNullable = new(false);

  #region IAutomationAction implementation

  /// <inheritdoc/>
  public string TemplateFamily { get; set; }

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
  public virtual IAutomationCondition Condition {
    get => _condition;
    set {
      if (_condition == value) {
        return;
      }
      if (_condition != null) {
        _condition.Listener = null;
      }
      _condition = value;
      if (_condition != null) {
        _condition.Listener = this;
      }
    }
  }
  IAutomationCondition _condition;

  /// <inheritdoc/>
  public abstract bool IsInErrorState { get; }

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

  static readonly PropertyKey<AutomationConditionBase> ConditionPropertyKey = new("Condition");
  static readonly PropertyKey<bool> IsMarkedForCleanupKey = new("IsMarkedForCleanup");
  static readonly PropertyKey<string> TemplateFamilyKey = new("TemplateFamily");

  /// <inheritdoc/>
  public virtual void LoadFrom(IObjectLoader objectLoader) {
    Condition = objectLoader.GetValueOrNull(ConditionPropertyKey, AutomationConditionBase.ConditionSerializerNullable);
    IsMarkedForCleanup = objectLoader.GetValueOrDefault(IsMarkedForCleanupKey);
    TemplateFamily = objectLoader.GetValueOrDefault(TemplateFamilyKey, null);
  }

  /// <inheritdoc/>
  public virtual void SaveTo(IObjectSaver objectSaver) {
    if (Condition is AutomationConditionBase condition) {
      objectSaver.Set(ConditionPropertyKey, condition, AutomationConditionBase.ConditionSerializer);
    }
    if (IsMarkedForCleanup) {
      objectSaver.Set(IsMarkedForCleanupKey, IsMarkedForCleanup);
    }
    if (!string.IsNullOrEmpty(TemplateFamily)) {
      objectSaver.Set(TemplateFamilyKey, TemplateFamily);
    }
  }

  #endregion

  #region API

  /// <inheritdoc/>
  public abstract string UiDescription { get; }

  /// <inheritdoc/>
  public virtual IAutomationAction CloneDefinition() {
    var clone = (AutomationActionBase)Activator.CreateInstance(GetType());
    clone.TemplateFamily = TemplateFamily;
    return clone;
  }

  /// <inheritdoc/>
  public virtual bool CheckSameDefinition(IAutomationAction other) {
    return other != null && other.GetType() == GetType();
  }

  /// <inheritdoc/>
  public abstract bool IsValidAt(AutomationBehavior behavior);

  /// <summary>
  /// Notifies that a new behavior has been assigned to the condition. It's the time to set up the behaviors. 
  /// </summary>
  /// <seealso cref="Behavior"/>
  protected abstract void OnBehaviorAssigned();

  /// <summary>
  /// Notifies that the current behavior is about to be cleared. It's the time to clean up the behaviors. 
  /// </summary>
  /// <seealso cref="Behavior"/>
  protected abstract void OnBehaviorToBeCleared();

  #endregion

  #region IAutomationConditionListener

  /// <inheritdoc/>
  public abstract void OnConditionState(IAutomationCondition automationCondition);

  #endregion

  #region Implementation

  /// <inheritdoc/>
  public override string ToString() {
    return $"TypeId={GetType()},Condition={Condition?.GetType()}";
  }

  #endregion
}

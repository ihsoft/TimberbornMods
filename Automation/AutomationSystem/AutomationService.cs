// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using IgorZ.Automation.TemplateTools;
using Timberborn.BaseComponentSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>Central point for all the automation related logic.</summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class AutomationService : IPostLoadableSingleton {

  #region Internal fields

  readonly HashSet<AutomationBehavior> _registeredBehaviors = new();
  readonly Color _highlightColor = Color.cyan * 0.5f;
  readonly Highlighter _highlighter;
  bool _highlightingEnabled;

  #endregion

  //FIXME: move to impl with the fields
  AutomationService(EventBus eventBus, Highlighter highlighter, BaseInstantiator baseInstantiator, ILoc loc) {
    EventBus = eventBus;
    BaseInstantiator = baseInstantiator;
    Loc = loc;
    eventBus.Register(this);
    _highlighter = highlighter;
  }

  #region IPostLoadableSingleton implemetation 

  /// <inheritdoc/>
  public void PostLoad() {}

  #endregion

  #region API

  /// <summary>Shortcut to the instantiator.</summary>
  public readonly BaseInstantiator BaseInstantiator;

  /// <summary>Shortcut to the localizator.</summary>
  public readonly ILoc Loc;

  /// <summary>Shortcut to EventBus.</summary>
  public readonly EventBus EventBus;

  /// <summary>Highlights all registered behaviors on the map.</summary>
  public void HighlightAutomationObjects(Color? useColor = null) {
    _highlightingEnabled = true;
    foreach (var behavior in _registeredBehaviors) {
      _highlighter.HighlightSecondary(behavior, useColor ?? _highlightColor);
    }
  }

  /// <summary>Resets highlightings.</summary>
  public void UnhighlightAutomationObjects() {
    _highlightingEnabled = false;
    _highlighter.UnhighlightAllSecondary();
  }

  /// <summary>Marks the behavior for removed rules verification.</summary>
  /// <remarks>The verification and cleanup will be done at the late update.</remarks>
  /// <seealso cref="CollectDeletedRules"/>
  public void MarkBehaviourForCleanup(AutomationBehavior behavior) {
    _behaviorsNeedsCleanup.Add(behavior);
    if (!_cleanupRulesComponent) {
      _cleanupRulesComponent = new GameObject("AutomationCleanupRules").AddComponent<CleanupComponent>();
      _cleanupRulesComponent.AutomationService = this;
    }
  }

  /// <summary>Removes the rules that are marked as deleted.</summary>
  /// <remarks>
  /// This method is automatically scheduled to the late update. However, it can be called directly to immediately
  /// clean up the deleted rules. It can be expensive, so it should be used with caution.
  /// </remarks>
  public void CollectDeletedRules() {
    DebugEx.Fine("Collecting deleted rules on {0} behaviors", _behaviorsNeedsCleanup.Count);
    foreach (var behavior in _behaviorsNeedsCleanup) {
      behavior.CollectCleanedRules();
    }
    _behaviorsNeedsCleanup.Clear();
  }

  #endregion

  #region Implementation

  sealed class CleanupComponent : MonoBehaviour {
    public AutomationService AutomationService;
    void LateUpdate() {
      AutomationService.CollectDeletedRules();
      Destroy(gameObject);
    }
  }

  CleanupComponent _cleanupRulesComponent;
  readonly HashSet<AutomationBehavior> _behaviorsNeedsCleanup = [];

  internal void RegisterBehavior(AutomationBehavior behavior) {
    _registeredBehaviors.Add(behavior);
    if (_highlightingEnabled) {
      _highlighter.HighlightSecondary(behavior, _highlightColor);
    }
  }

  internal void UnregisterBehavior(AutomationBehavior behavior) {
    if (_highlightingEnabled) {
      _highlighter.UnhighlightSecondary(behavior);
    }
    _registeredBehaviors.Remove(behavior);
  }

  /// <summary>
  /// Activates highlighting of the automated objects if a <see cref="IAutomationModeEnabler"/> tool is activated.
  /// </summary>
  [OnEvent]
  public void OnToolEntered(ToolEnteredEvent toolEnteredEvent) {
    if (toolEnteredEvent.Tool is not IAutomationModeEnabler) {
      return;
    }
    HighlightAutomationObjects();
  }

  /// <summary>
  /// Deactivates highlighting of the automated objects if a <see cref="IAutomationModeEnabler"/> tool is deactivated.
  /// </summary>
  [OnEvent]
  public void OnToolExited(ToolExitedEvent toolExitedEvent) {
    UnhighlightAutomationObjects();
  }

  #endregion
}
// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using IgorZ.Automation.Settings;
using IgorZ.Automation.TemplateTools;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Localization;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.ToolSystem;
using Timberborn.UILayoutSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.AutomationSystem;

/// <summary>Central point for all the automation related logic.</summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class AutomationService : ITickableSingleton, ILoadableSingleton {

  #region ITickableSingleton implementation

  /// <inheritdoc/>
  public void Tick() {
    CurrentTick++;
    // ReSharper disable once ForCanBeConvertedToForeach
    for (var i = 0; i < _tickables.Count; i++) {
      _tickables[i].Invoke(CurrentTick);
    }
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {
    EventBus.Register(this);
  }

  #endregion

  #region API

  /// <summary>Ticks since the game load.</summary>
  /// <remarks>Can be used for synchronization and delaying actions.</remarks>
  public static int CurrentTick { get; private set; }

  /// <summary>Indicates if the game is fully loaded.</summary>
  /// <remarks>
  /// In this state, all the game loading and initialization logic is done, but the automation system is not yet ready to
  /// normally process signals.
  /// </remarks>
  /// <seealso cref="AutomationServiceReadyEvent"/>
  public static bool GameLoaded { get; private set; }

  /// <summary>Indicates if the automation system is ready to use.</summary>
  /// <remarks>In this state, all actions are loaded, initialized and synchronized.</remarks>
  public static bool AutomationSystemReady { get; private set; }

  /// <summary>Shortcut to the instantiator.</summary>
  public readonly BaseInstantiator BaseInstantiator;

  /// <summary>Shortcut to the localizator.</summary>
  public readonly ILoc Loc;

  /// <summary>Shortcut to EventBus.</summary>
  public readonly EventBus EventBus;

  /// <summary>Highlights all registered behaviors on the map.</summary>
  public void HighlightAutomationObjects(Color? useColor = null) {
    _highlightingEnabled = true;
    foreach (var behavior in _blockObjectToBehaviorMap.Values) {
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

  /// <summary>Registers a tickable action.</summary>
  public void RegisterTickable(Action<int> tickable) {
    if (_tickables.Contains(tickable)) {
      throw new InvalidOperationException("Tickable already registered: " + tickable);
    }
    _tickables.Add(tickable);
  }

  /// <summary>Unregisters a tickable action.</summary>
  public void UnregisterTickable(Action<int> tickable) {
    if (!_tickables.Remove(tickable)) {
      throw new InvalidOperationException("Tickable not registered: " + tickable);
    }
  }

  #endregion

  #region Implementation

  readonly List<Action<int>> _tickables = [];

  sealed class CleanupComponent : MonoBehaviour {
    public AutomationService AutomationService;
    void LateUpdate() {
      AutomationService.CollectDeletedRules();
      Destroy(gameObject);
    }
  }

  readonly Dictionary<BlockObject, AutomationBehavior> _blockObjectToBehaviorMap = new();
  readonly HashSet<AutomationBehavior> _behaviorsNeedsCleanup = [];
  readonly Color _highlightColor = Color.cyan * 0.5f;
  readonly Highlighter _highlighter;

  bool _highlightingEnabled;
  CleanupComponent _cleanupRulesComponent;

  AutomationService(EventBus eventBus, Highlighter highlighter, BaseInstantiator baseInstantiator, ILoc loc) {
    EventBus = eventBus;
    BaseInstantiator = baseInstantiator;
    Loc = loc;
    _highlighter = highlighter;
    CurrentTick = 0;
    GameLoaded = false;
    AutomationSystemReady = false;
  }

  internal void RegisterBehavior(AutomationBehavior behavior) {
    _blockObjectToBehaviorMap[behavior.BlockObject] = behavior;
    if (_highlightingEnabled) {
      _highlighter.HighlightSecondary(behavior, _highlightColor);
    }
  }

  internal void UnregisterBehavior(AutomationBehavior behavior) {
    if (_highlightingEnabled) {
      _highlighter.UnhighlightSecondary(behavior);
    }
    if (!_blockObjectToBehaviorMap.Remove(behavior.BlockObject)) {
      DebugEx.Warning("Failed to unregister behavior {0}. It was not registered.", behavior);
    }
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

  /// <summary>
  /// Reacts on the <see cref="EnteredFinishedStateEvent"/> to update rules that work on finished buildings only.
  /// </summary>
  [OnEvent]
  public void OnEnteredFinishedStateEvent(EnteredFinishedStateEvent evt) {
    if (!AutomationSystemReady) {
      return;  // Only serve event in the loaded and active game.
    }
    if (!_blockObjectToBehaviorMap.TryGetValue(evt.BlockObject, out var behavior)) {
      return;
    }

    // Update rules that work on finished building only.
    foreach (var action in behavior.Actions) {
      if (!action.Condition.CanRunOnUnfinishedBuildings) {
        action.Condition.Activate(); 
      }
    }
  }

  #endregion

  #region Game load callback

  /// <summary>Called when the game initialized.</summary>
  [OnEvent]
  public void OnNewGameInitialized(ShowPrimaryUIEvent newGameInitializedEvent) {
    GameLoaded = true;
    EventBus.Post(new GameLoadedEvent());

    if (!AutomationDebugSettings.ReevaluateRulesOnLoad) {
      BindLoadedRules();
    } else {
      ReexecuteLoadedRules();
    }

    DebugEx.Info("[Automation system] Loaded and ready");
    AutomationSystemReady = true;
    EventBus.Post(new AutomationServiceReadyEvent());
  }

  void BindLoadedRules() {
    var activatedRulesCount = 0;
    foreach (var behavior in _blockObjectToBehaviorMap.Values) {
      foreach (var action in behavior.Actions) {
        action.Condition.Behavior = behavior;
        action.Behavior = behavior;
        action.Condition.Activate(noTrigger: true);
        activatedRulesCount++;
      }
    }
    if (activatedRulesCount > 0) {
      DebugEx.Info("[Automation system] Activated {0} rules on {1} behaviors",
                   activatedRulesCount, _blockObjectToBehaviorMap.Count);
    } else {
      DebugEx.Info("[Automation system] No rules to activate");
    }
  }

  void ReexecuteLoadedRules() {
    DebugEx.Warning("[Automation system] Re-evaluating all rules on game load.");
    // Need a copy since the rules be removed/added, which triggers the registration updates.
    var behaviors = _blockObjectToBehaviorMap.Values.ToList();
    foreach (var behavior in behaviors) {
      var actions = behavior.Actions.ToList();
      behavior.ClearAllRules();
      foreach (var action in actions) {
        behavior.AddRule(action.Condition, action);
      }
    }
  }

  #endregion
}

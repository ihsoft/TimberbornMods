// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using Timberborn.Localization;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.Actions;

sealed class ScriptedAction : AutomationActionBase {

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new ScriptedAction { TemplateFamily = TemplateFamily };
  }

  /// <inheritdoc/>
  public override string UiDescription => _uiDescription;
  string _uiDescription;

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    //FIXME: parse te scriptable anme and check rtestricted type if any.
    return true;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (Condition.ConditionState) {
      _actionExecutor();
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    try {
      ParseAction();
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "Failed to parse action: " + e.Message);
      IsMarkedForCleanup = true;
    }
  }

  #endregion

  #region API

  /// <summary>Action to execute.</summary>
  /// <remarks>It is a string representation of the action setup. For example, "Floodgate.SetHeight(150)".</remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public string SerializedAction { get; private set; }

  /// <summary>Sets the expression conditions.</summary>
  /// <remarks>Can only be set on the non-active condition.</remarks>
  /// <seealso cref="SerializedAction"/>
  public void SetAction(string action) {
    if (Behavior) {
      throw new InvalidOperationException("Cannot change action when the behavior is assigned.");
    }
    SerializedAction = action;
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<string> SerializedActionKey = new("SerializedAction");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    SerializedAction = objectLoader.Get(SerializedActionKey);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(SerializedActionKey, SerializedAction);
  }

  #endregion

  #region Implementation

  static readonly Regex ActionRegex = new(@"^([a-zA-Z.]+)\((.*)\)$");
  Action _actionExecutor;

  void ParseAction() {
    var match = ActionRegex.Match(SerializedAction);
    if (!match.Success) {
      throw new ScriptError("Invalid action format: " + SerializedAction);
    }
    var actionName = match.Groups[1].Value;
    var argument = match.Groups[2].Value;
    _actionExecutor = ScriptingService.Instance.GetActionExecutor(actionName, Behavior, argument.Split(','));

    var actionDef = ScriptingService.Instance.GetActionDefinition(actionName);
    if (actionDef.ArgumentTypes.Length > 1) {
      //FIXME: support multiple arguments one day.
      throw new ScriptError("Cannot handle multiple arguments in action: " + actionDef.Name);
    }
    var description = actionDef.DisplayName;
    if (actionDef.ArgumentTypes.Length > 0) {
      var argumentDef = actionDef.ArgumentTypes[0];
      var argumentValue = argument;
      if (argumentDef.Options != null) {
        argumentValue = argumentDef.Options
            .Where(x => x.Value == argument)
            .Select(x => x.Text)
            .FirstOrDefault();
        if (argumentValue == null) {
          throw new ScriptError($"Cannot find option for '{argument}' in {actionDef.Name}");
        }
      }
      if (argumentDef.ArgumentType != IScriptable.ArgumentDef.Type.String) {
        argumentValue = (int.Parse(argument) / 100f).ToString("0.##");
      }
      description += " " + argumentValue;
    }
    _uiDescription = TextColors.ColorizeText($"<SolidHighlight>{description}</SolidHighlight>");
  }

  #endregion
}

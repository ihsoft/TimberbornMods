// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine;
using Timberborn.Localization;
using Timberborn.Persistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.Conditions;

sealed class ScriptedCondition : AutomationConditionBase {

  const string AndOperatorLocString = "IgorZ.Automation.Conditions.AndOperator";

  #region AutomationConditionBase overrides

  /// <inheritdoc/>
  public override string UiDescription => _uiDescription;
  string _uiDescription;

  /// <inheritdoc/>
  public override void SyncState() {
    CheckOperands();
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    try {
      ParseConditions();
    } catch (ScriptError e) {
      HostedDebugLog.Error(Behavior, "Failed to parse conditions: " + e.Message);
      IsMarkedForCleanup = true;
    }
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    Dispose();
  }

  /// <inheritdoc/>
  public override IAutomationCondition CloneDefinition() {
    return new ScriptedCondition { Conditions = Conditions };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    //FIXME: somehow check condition?
    return true;
  }

  #endregion

  #region API

  /// <summary>Conditions to check.</summary>
  /// <remarks>
  /// Conditions are checked for the term "AND". For example, the following string:
  /// <code>
  /// [
  ///   "Weather.Season=drought",
  ///   "Floodgate.Height>0.5",
  /// ]
  /// </code>
  /// will evaluate to expression: "Weather.Season=Drought AND Floodgate.Height>0.5".
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public List<string> Conditions { get; private set; }

  /// <summary>Sets the expression conditions.</summary>
  /// <remarks>Can only be set on the non-active condition.</remarks>
  /// <seealso cref="Conditions"/>
  public void SetConditions(IEnumerable<string> operands) {
    if (Behavior) {
      throw new InvalidOperationException("Cannot change conditions when the behavior is assigned.");
    }
    Conditions = operands.ToList();
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly ListKey<string> ConditionsKey = new("Conditions");

  /// <inheritdoc/>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Conditions = objectLoader.Get(ConditionsKey);
  }

  /// <inheritdoc/>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(ConditionsKey, Conditions);
  }

  #endregion

  #region Implementation

  static readonly Regex ConditionRegex = new("([a-zA-Z.]+)([=<>]+)(.+)");
  readonly List<Operand> _operands = [];

  void ParseConditions() {
    var operandsDescriptions = new List<string>();
    foreach (var serializedOperand in Conditions) {
      var (name, op, argument) = ParseCondition(serializedOperand);

      var triggerDef = ScriptingService.Instance.GetTriggerDefinition(name, Behavior);
      var argumentValue = argument;
      if (triggerDef.ResultType.Options != null) {
        argumentValue = triggerDef.ResultType.Options
            .Where(x => x.Value == argument)
            .Select(x => x.Text)
            .FirstOrDefault();
        if (argumentValue == null) {
          throw new ScriptError($"Cannot find option for '{argument}' in {triggerDef.FullName}");
        }
      }

      var trigger = ScriptingService.Instance.GetTriggerSource(name, Behavior, CheckOperands);
      try {
        var operand = triggerDef.ResultType.ValueType == ScriptValue.TypeEnum.String
            ? Operand.ForStringArgument(trigger, op, argument)
            : Operand.ForNumberArgument(trigger, op, argument);
        _operands.Add(operand);
      } catch (ScriptError) {
        trigger.Dispose();
        throw;
      }
      operandsDescriptions.Add($"<SolidHighlight>{triggerDef.DisplayName} {op} {argumentValue}</SolidHighlight>");
    }
    _uiDescription = TextColors.ColorizeText(string.Join(Behavior.Loc.T(AndOperatorLocString), operandsDescriptions));
  }

  void Dispose() {
    foreach (var op in _operands) {
      op.TriggerSourceSource.Dispose();
    }
    _operands.Clear();
  }

  void CheckOperands() {
    ConditionState = _operands.All(line => line.Execute());
  }

  static (string name, string op, string argument) ParseCondition(string operand) {
    var match = ConditionRegex.Match(operand);
    if (!match.Success) {
      throw new ScriptError("Invalid condition: " + operand);
    }
    return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
  }

  #endregion

  #region Warpper for the operand checker

  class Operand {
    public readonly ITriggerSource TriggerSourceSource;
    public readonly Func<bool> Execute;

    /// <summary>Creates operand where rvalue is a text constant.</summary>
    /// <exception cref="ScriptError">if the op is not recognized.</exception>
    public static Operand ForStringArgument(ITriggerSource triggerSource, string op, string argument) {
      return new Operand(triggerSource, op, argument);
    }

    /// <summary>Creates operand where rvalue is a numeric constant.</summary>
    /// <exception cref="ScriptError">if argument cannot be parsed as number or the op is not recognized.</exception>
    public static Operand ForNumberArgument(ITriggerSource triggerSource, string op, string argument) {
      int fixedFloat;
      try {
        fixedFloat = Mathf.RoundToInt(float.Parse(argument) * 100);
      } catch (FormatException) {
        throw new ScriptError("Cannot parse numeric argument: " + argument);
      }
      return new Operand(triggerSource, op, fixedFloat);
    }

    Operand(ITriggerSource triggerSource, string op, string argument) {
      TriggerSourceSource = triggerSource;
      Execute = op switch {
          "=" => () => triggerSource.CurrentValue.AsString == argument,
          "<>" => () => triggerSource.CurrentValue.AsString != argument,
          _ => throw new ScriptError("Unsupported string operand: " + op),
      };
    }

    Operand(ITriggerSource triggerSource, string op, int argument) {
      TriggerSourceSource = triggerSource;
      Execute = op switch {
          "=" => () => triggerSource.CurrentValue.AsNumber == argument,
          "<>" => () => triggerSource.CurrentValue.AsNumber != argument,
          "<" => () => triggerSource.CurrentValue.AsNumber < argument,
          "<=" => () => triggerSource.CurrentValue.AsNumber <= argument,
          ">" => () => triggerSource.CurrentValue.AsNumber > argument,
          ">=" => () => triggerSource.CurrentValue.AsNumber >= argument,
          _ => throw new ScriptError("Unsupported numeric operand: " + op),
      };
    }
  }

  #endregion
}

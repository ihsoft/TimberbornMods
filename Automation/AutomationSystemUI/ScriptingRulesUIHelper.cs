using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.Common;
using Timberborn.Localization;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.AutomationSystemUI;

class ScriptingRulesUIHelper {
  const string BuildingSignalSourceLocKey = "IgorZ.Automation.AutomationFragment.BuildingSignalSource";

  #region API

  public record BuildingSignal {
    public string Describe => DescribeFn();
    public string SignalName { get; init; }
    public string ExportedSignalName { get; init; }
    public bool IsActive { get; init; }
    internal Func<string> DescribeFn { get; init; }
  }

  public IReadOnlyList<BuildingSignal> BuildingSignals => _buildingSignals;

  public int ExposedSignalsCount { get; private set; }
  public int RulesCount { get; private set; }

  public void SetBuilding(AutomationBehavior automationBehavior) {
    _buildingSignals.Clear();
    if (automationBehavior == null) {
      return;
    }
    var signalMappings = new Dictionary<string, string>();
    RulesCount = 0;
    foreach (var action in automationBehavior.Actions) {
      var signalMapping = TryGetSignalMapping(action as ScriptedAction);
      if (signalMapping.buildingSignal == null) {
        RulesCount++;
      } else {
        signalMappings[signalMapping.buildingSignal] = signalMapping.customSignal;
      }
    }
    var signals = _scriptingService.GetSignalNamesForBuilding(automationBehavior)
        .Where(x => !NonBuildingActions.Any(x.StartsWith));
    ExposedSignalsCount = 0;
    foreach (var signalName in signals) {
      var signalMapping = signalMappings.GetOrDefault(signalName);
      if (signalMapping != null) {
        ExposedSignalsCount++;
      }
      var signalDef = _scriptingService.GetSignalDefinition(signalName, automationBehavior);
      var signalSourceFn = _scriptingService.GetSignalSource(signalName, automationBehavior);
      _buildingSignals.Add(new BuildingSignal {
          SignalName = signalName,
          DescribeFn = () => GetFormattedSignalValue(signalDef, signalSourceFn),
          ExportedSignalName = signalMapping,
          IsActive = true, //FIXME: get actual active action state
      });
    }
  }
 
  /// <summary>Indicates if the action is a signal mapping action.</summary>
  /// <remarks>
  /// A signal mapping action is an action that maps a building signal to a custom signal name. It's a rule, defined
  /// like: "if signal=signal, then set signal 'customName'".
  /// </remarks>
  public static bool IsSignalMapping(IAutomationAction action) {
    if (action.Condition is not ScriptedCondition || action is not ScriptedAction scriptedAction) {
      return false;
    }
    return TryGetSignalMapping(scriptedAction).buildingSignal != null;
  }

  #endregion

  #region Implementation

  // FIXME: Addd "District.". Or not.
  // FIXME: add scope to the signal definition and filter by that
  static readonly List<string> NonBuildingActions = [
      "Debug.", "Weather.", "Signals.", "District.",
  ];

  readonly ScriptingService _scriptingService;
  readonly ILoc _loc;

  readonly List<BuildingSignal> _buildingSignals = new();

  ScriptingRulesUIHelper(ScriptingService scriptingService, ILoc loc) {
    _scriptingService = scriptingService;
    _loc = loc;
  }

  static (string buildingSignal, string customSignal) TryGetSignalMapping(ScriptedAction scriptedAction) {
    var action = scriptedAction.ParsedExpression;
    var condition = ((ScriptedCondition)scriptedAction.Condition).ParsedExpression;
    // Ready for collection shows up as a singla mapping! 
    if (action is not { ActionName: "Signals.Set" }
        || action.Operands[1] is not ConstantValueExpr { ValueType: ScriptValue.TypeEnum.String } actionExpr
        || action.Operands[2] is not SignalOperator actionSignalOperator
        || condition is not BinaryOperator {
            Name: "eq", Left: SignalOperator leftSignalOperator, Right: SignalOperator rightSignalOperator,
        }
        || leftSignalOperator.SignalName != rightSignalOperator.SignalName
        || leftSignalOperator.SignalName != actionSignalOperator.SignalName) {
      return (null, null);
    }
    return (actionSignalOperator.SignalName, actionExpr.ValueFn().AsString);
  }

  string GetFormattedSignalValue(SignalDef signalDef, Func<ScriptValue> signalSourceFn) {
    var valueStr = signalDef.Result.ValueFormatter != null
        ? signalDef.Result.ValueFormatter(signalSourceFn())
        : signalSourceFn().AsFloat.ToString("0.#");
    return _loc.T(BuildingSignalSourceLocKey, signalDef.DisplayName, valueStr);
  }

  #endregion
}

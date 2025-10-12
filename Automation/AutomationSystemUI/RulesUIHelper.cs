// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine;
using IgorZ.Automation.ScriptingEngine.Parser;
using Timberborn.Localization;

namespace IgorZ.Automation.AutomationSystemUI;

class RulesUIHelper {
  const string BuildingSignalSourceLocKey = "IgorZ.Automation.AutomationFragment.BuildingSignalSource";

  #region API

  /// <summary>Information about a building signal that can be mapped to a custom signal.</summary>
  public record BuildingSignal {
    public string Describe => DescribeFn();
    public string SignalName { get; init; }
    public string ExportedSignalName { get; init; }
    public ScriptedAction Action { get; init; }
    internal Func<string> DescribeFn { get; init; }
  }

  /// <summary>List of building signals that can be mapped to custom signals.</summary>
  /// <remarks>
  /// This list can be longer than the building actually has signals! It happens when the scripts have more than one
  /// mapping for the same signal. This situation can be introduced via scripts editor or import features. The regular
  /// signals UI won't let it happen.
  /// </remarks>
  public IReadOnlyList<BuildingSignal> BuildingSignals => _buildingSignals;
  readonly List<BuildingSignal> _buildingSignals = [];

  /// <summary>Number of building signals that are mapped to a custom signal.</summary>
  /// <seealso cref="BuildingSignals"/>
  public int ExposedSignalsCount { get; private set; }

  /// <summary>List of rules on the building, except the signal mapping rules.</summary>
  /// <remarks>The signals mappings are regular rules. However, they are excluded from this list</remarks>
  /// <seealso cref="BuildingSignals"/>
  public IReadOnlyList<IAutomationAction> BuildingRules => _buildingRules;
  readonly List<IAutomationAction> _buildingRules = [];

  /// <summary>The building that is being edited.</summary>
  public AutomationBehavior AutomationBehavior { get; private set; }

  /// <summary>List of all building signal names that can be mapped to custom signals.</summary>
  /// <remarks>
  /// This list is not a complete list of the building specific signals. It's refined for the UI purpose. It can have
  /// more or less signals than the <see cref="ScriptingService.GetSignalNamesForBuilding"/> could return. 
  /// </remarks>
  public IReadOnlyList<string> BuildingSignalNames => _buildingSignalNames;
  readonly List<string> _buildingSignalNames = [];

  /// <summary>Sets the building that is being edited.</summary>
  /// <remarks>All the internal state is reset.</remarks>
  public void SetBuilding(AutomationBehavior automationBehavior) {
    AutomationBehavior = automationBehavior;
    _buildingSignals.Clear();
    _buildingRules.Clear();
    ExposedSignalsCount = 0;
    if (!automationBehavior) {
      return;
    }

    // We want to keep a stable order of signals, so list them in the order defined by the components.
    // However, there can be multiple mappings for the same building signal.
    _buildingSignalNames.Clear();
    foreach (var signalName in _scriptingService.GetSignalNamesForBuilding(automationBehavior)) {
      if (NonBuildingActions.Any(signalName.StartsWith) || signalName.Contains(".OnUnfinished")) {
        continue; // Not what we want to bind as an exported signal.
      }
      var signalDef = _scriptingService.GetSignalDefinition(signalName, automationBehavior);
      if (signalDef.Result.ValueType != ScriptValue.TypeEnum.Number) {
        continue; // Custom signals can only be numbers.
      }
      _buildingSignalNames.Add(signalName);
    }
    var mappings = new List<(string signalName, ScriptedAction action)>();
    foreach (var action in automationBehavior.Actions) {
      var (buildingSignalName, _) = TryGetSignalMapping(action as ScriptedAction);
      if (buildingSignalName == null) {
        _buildingRules.Add(action);
        continue;
      }
      mappings.Add((buildingSignalName, action as ScriptedAction));
    }
    foreach (var buildingSignalName in _buildingSignalNames) {
      var signalDef = _scriptingService.GetSignalDefinition(buildingSignalName, automationBehavior);
      if (signalDef.Result.ValueType != ScriptValue.TypeEnum.Number) {
        continue; // Custom signals can only be numbers.
      }
      var signalSourceFn = _scriptingService.GetSignalSource(buildingSignalName, automationBehavior);
      var existingMappings = mappings.Where(x => x.signalName == buildingSignalName).ToList();
      mappings.RemoveAll(x => x.signalName == buildingSignalName);
      var describeFn = () => GetFormattedSignalValue(signalDef, signalSourceFn);
      if (existingMappings.Count == 0) {
        _buildingSignals.Add(new BuildingSignal {
            SignalName = buildingSignalName,
            DescribeFn = describeFn,
            ExportedSignalName = null,
            Action = null,
        });
        continue;
      }
      foreach (var existingMapping in existingMappings) {
        ExposedSignalsCount++;
        _buildingSignals.Add(new BuildingSignal {
            SignalName = buildingSignalName,
            DescribeFn = describeFn,
            ExportedSignalName = TryGetSignalMapping(existingMapping.action).customSignal,
            Action = existingMapping.action,
        });
      }
    }
    // Process any remaining mappings that refer to non-building signals (added via rules import).
    foreach (var (buildingSignalName, action) in mappings) {
      var signalDef = _scriptingService.GetSignalDefinition(buildingSignalName, automationBehavior);
      var signalSourceFn = _scriptingService.GetSignalSource(buildingSignalName, automationBehavior);
      ExposedSignalsCount++;
      _buildingSignals.Add(new BuildingSignal {
          SignalName = buildingSignalName,
          DescribeFn = () => GetFormattedSignalValue(signalDef, signalSourceFn),
          ExportedSignalName = TryGetSignalMapping(action).customSignal,
          Action = action,
      });
    }
  }

  /// <summary>Creates a rule for the signal mapping.</summary>
  /// <remarks>
  /// It's just a helper method. The signal mapping is a regular scripted automation rule that looks like this:
  /// <code>
  /// If: (eq (sig BuildingSignalName) (sig BuildingSignalName))
  /// Then: (act Signals.Set 'ExportedSignalName' (sig BuildingSignalName))
  /// </code>
  /// </remarks>
  public void SetExportedSignalName(string buildingSignalName, string exportedSignalName) {
    if (!_buildingSignalNames.Contains(buildingSignalName)) {
      throw new ArgumentException($"Signal '{buildingSignalName}' is not available on the building.");
    }
    var condition = new ScriptedCondition();
    condition.SetExpression($"(eq (sig {buildingSignalName}) (sig {buildingSignalName}))");
    var action = new ScriptedAction();
    action.SetExpression($"(act Signals.Set '{exportedSignalName}' (sig {buildingSignalName}))");
    AutomationBehavior.AddRule(condition, action);
  }

  /// <summary>Removes all signal mapping rules from the building.</summary>
  /// <seealso cref="BuildingSignals"/>
  public void ClearSignalsOnBuilding() {
    foreach (var signal in _buildingSignals.Where(x => x.Action != null)) {
      AutomationBehavior.DeleteRule(signal.Action);
    }
  }

  /// <summary>Removes all rules from the building, except the signal mapping rules.</summary>
  /// <seealso cref="BuildingRules"/>
  public void ClearRulesOnBuilding() {
    foreach (var rule in _buildingRules) {
      AutomationBehavior.DeleteRule(rule);
    }
  }

  #endregion

  #region Implementation

  // FIXME: Add scope to the signal definition and filter by it.
  static readonly List<string> NonBuildingActions = [
      "Debug.", "Weather.", "Signals.", "District.",
  ];

  readonly ScriptingService _scriptingService;
  readonly ILoc _loc;

  RulesUIHelper(ScriptingService scriptingService, ILoc loc) {
    _scriptingService = scriptingService;
    _loc = loc;
  }

  /// <summary>Verifies if the scripted action is a signal mapping and extracts the signal names.</summary>
  /// <seealso cref="SetExportedSignalName"/>
  static (string buildingSignal, string customSignal) TryGetSignalMapping(ScriptedAction scriptedAction) {
    var action = scriptedAction.ParsingResult.ParsedExpression as ActionOperator;
    var condition = ((ScriptedCondition)scriptedAction.Condition).ParsingResult.ParsedExpression;
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
        : signalSourceFn().AsFloat.ToString("0.##");
    return _loc.T(BuildingSignalSourceLocKey, signalDef.DisplayName, valueStr);
  }

  #endregion
}

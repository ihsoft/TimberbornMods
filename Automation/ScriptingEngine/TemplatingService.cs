// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.TimberDev.UI;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine;

class TemplatingService {

  const string IncompleteDeclarationErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.IncompleteDeclarationError";
  const string UnclosedMultiLineCommentErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.UnclosedMultiLineComment";
  const string ExpectedConditionErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.ExpectedConditionError";
  const string ExpectedActionErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.ExpectedActionError";
  const string ConditionNotApplicableErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.ConditionNotApplicableError";
  const string ActionNotApplicableErrorLocKey = "IgorZ.Automation.Scripting.ImportRules.ActionNotApplicableError";

  const string TemplatePrefix = "template:";
  const string PreconditionPrefix = "precondition:";
  const string ConditionPrefix = "condition:";
  const string ActionPrefix = "action:";

  #region API

  /// <summary>Thrown when an error occurs during the import of automation rules.</summary>
  public sealed class ImportError(int lineNum, string text) : Exception($"Import error at line {lineNum}: {text}") {
    /// <summary>Line number where the error occurred.</summary>
    public int LineNum { get; } = lineNum;
    /// <summary>Localized message describing the error.</summary>
    public string Text { get; } = text;
  }

  /// <summary>Parses the provided text into a list of automation actions.
  /// </summary>
  /// <exception cref="ImportError">if the text can't be successfully parsed</exception>
  /// <seealso cref="RenderRulesToText"/>
  public List<IAutomationAction> ParseFromText(
      string text, AutomationBehavior activeBuilding, bool allowErrors, bool skipFailedRules, out int skippedRules) {
    var rules = new List<IAutomationAction>();
    skippedRules = 0;
    var lineNumber = 0;
    var lines = text.Split(['\n']);
    while (lineNumber < lines.Length) {
      var line = TakeNextLine(lines, ref lineNumber, dontFail: true);
      if (line == null) {
        break; // No more lines to process
      }
      var condition = new ScriptedCondition();
      var action = new ScriptedAction();
      if (line.StartsWith(TemplatePrefix)) {
        action.TemplateFamily = line[TemplatePrefix.Length..];
        line = TakeNextLine(lines, ref lineNumber);
      }
      if (line.StartsWith(PreconditionPrefix)) {
        condition.Precondition = line[PreconditionPrefix.Length..];
        line = TakeNextLine(lines, ref lineNumber);
      }

      var conditionLineNumber = lineNumber;
      if (!line.StartsWith(ConditionPrefix)) {
        throw new ImportError(conditionLineNumber, _uiFactory.T(ExpectedConditionErrorLocKey));
      }
      var conditionExpression = line[ConditionPrefix.Length..];

      line = TakeNextLine(lines, ref lineNumber);
      var actionLineNumber = lineNumber;
      if (!line.StartsWith(ActionPrefix)) {
        throw new ImportError(actionLineNumber, _uiFactory.T(ExpectedActionErrorLocKey));
      }
      var actionExpression = line[ActionPrefix.Length..];

      condition.SetExpression(conditionExpression);
      if (!condition.IsValidAt(activeBuilding) && !allowErrors) {
        if (!skipFailedRules) {
          throw new ImportError(
              conditionLineNumber, _uiFactory.T(ConditionNotApplicableErrorLocKey, conditionExpression));
        }
        skippedRules++;
        continue; // Skip invalid conditions
      }

      action.SetExpression(actionExpression);
      if (!action.IsValidAt(activeBuilding) && !allowErrors) {
        if (!skipFailedRules) {
          throw new ImportError(actionLineNumber, _uiFactory.T(ActionNotApplicableErrorLocKey, actionExpression));
        }
        skippedRules++;
        continue; // Skip invalid actions
      }

      action.Condition = condition;
      rules.Add(action);
    }
    return rules;
  }

  /// <summary>Renders the automation rules to a text format that can be imported later.</summary>
  /// <seealso cref="ParseFromText"/>
  public string RenderRulesToText(IList<IAutomationAction> actions) {
    var text = new List<string>();
    foreach (var action in actions) {
      if (action.Condition is not ScriptedCondition scriptedCondition || action is not ScriptedAction scriptedAction) {
        DebugEx.Warning("Ignoring non-scripted action: {0}", action);
        continue;
      }
      if (action.TemplateFamily != null) {
        text.Add($"{TemplatePrefix}{action.TemplateFamily}");
      }
      if (scriptedCondition.Precondition != null) {
        text.Add($"{PreconditionPrefix}{scriptedCondition.Precondition}");
      }
      text.Add($"{ConditionPrefix}{scriptedCondition.Expression}");
      text.Add($"{ActionPrefix}{scriptedAction.Expression}");
      text.Add(""); // Add an empty line to separate rules
    }
    return string.Join("\n", text).TrimEnd('\n');
  }

  #endregion

  #region Implementation

  readonly UiFactory _uiFactory;

  TemplatingService(UiFactory uiFactory) {
    _uiFactory = uiFactory;
  }

  string TakeNextLine(string[] lines, ref int index, bool dontFail = false) {
    string multiLineCommentCloseTag = null;
    while (index < lines.Length) {
      var line = lines[index++].Trim();
      if (line.Length == 0) {
        continue; // Skip empty lines
      }
      if (multiLineCommentCloseTag != null) {
        if (line.EndsWith(multiLineCommentCloseTag)) {
          multiLineCommentCloseTag = null;  // Close the multi-line comment
        }
        continue;
      }
      if (line.StartsWith("/*")) {
        if (!line.EndsWith("*/")) {
          multiLineCommentCloseTag = "*/";
        }
        continue;
      }
      if (line.StartsWith("#|")) {
        if (!line.EndsWith("|#")) {
          multiLineCommentCloseTag = "|#";
        }
        continue;
      }
      if (!line.StartsWith("#") && !line.StartsWith("//") && !line.StartsWith(";")) {
        return line; // Return the first non-empty, non-comment line
      }
    }
    if (multiLineCommentCloseTag != null) {
      DebugEx.Warning("Multi-line comment not closed at {0}", index);
      throw new ImportError(index, _uiFactory.T(UnclosedMultiLineCommentErrorLocKey));
    }
    if (dontFail) {
      return null; // No more lines to process
    }
    DebugEx.Warning("More lines requested, but none available at {0}", index);
    throw new ImportError(index, _uiFactory.T(IncompleteDeclarationErrorLocKey));
  }

  #endregion
}

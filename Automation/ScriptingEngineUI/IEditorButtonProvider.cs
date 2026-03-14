// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngineUI;

/// <summary>A basic interface for the components that inject buttons to the rules editor dialog.</summary>
/// <remarks>
/// The component can specify an optional button that is used to create a new row. And an optional button that will be
/// visible for every row. All providers need to be bound via <c>MutiBind</c>. The order of binding will determine the
/// order of appearance in UI.
/// </remarks>
interface IEditorButtonProvider {
  /// <summary>LocKey of the button that creates a new rule row. If <c>null</c>, then button is not shown.</summary>
  /// <remarks>When a new row is created, the <see cref="OnRuleRowBtnAction"/> is immediately called for it.</remarks>
  public string CreateRuleBtnLocKey { get; }

  /// <summary>
  /// LocKey of the button to present on every rule row. If <c>null</c>, then button is not shown.
  /// </summary>
  public string RuleRowBtnLocKey { get; }

  /// <summary>The action that is called when the rule row button is clicked or when a new row is created.</summary>
  /// <param name="ruleRow">The rule for which the action is being executed.</param>
  public void OnRuleRowBtnAction(RuleRow ruleRow);

  /// <summary>Tells if the rule row action is available for the row.</summary>
  /// <remarks>This check is done when the rule row view is activated.</remarks>
  /// <param name="ruleRow">The rule row to check teh status for.</param>
  /// <returns></returns>
  public bool IsRuleRowBtnEnabled(RuleRow ruleRow);
}

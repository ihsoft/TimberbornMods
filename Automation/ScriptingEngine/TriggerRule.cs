// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.ScriptingEngine.Nodes;

namespace IgorZ.Automation.ScriptingEngine;

/// <summary>Rule that listens to a trigger and executes a set of statements when the trigger is fired.</summary>
class TriggerRule : ITriggerEventListener {

  #region ITriggerEventListener implementation

  /// <inheritdoc/>
  public ITrigger Trigger { get; }

  /// <inheritdoc/>
  public string Name { get; }

  /// <inheritdoc/>
  public IExpressionValue[] Args { get; }

  /// <inheritdoc/>
  public void OnEvent() {
    foreach (var function in _statements) {
      function.Eval();
    }
  }

  /// <inheritdoc/>
  public void OnTriggerDestroyed() {
    Trigger.UnregisterListener(this);
  }

  #endregion

  #region API

  /// <summary>Adds a statement to the rule. They're executed in order when the trigger sends an event.</summary>
  public void AddStatement(ExpressionNode statement) {
    _statements.Add(statement);
  }

  #endregion

  #region Implementation

  readonly List<ExpressionNode> _statements = [];

  public TriggerRule(ITrigger trigger, string eventName, IExpressionValue[] args) {
    Name = eventName;
    Args = args;
    Trigger = trigger;
    trigger.RegisterListener(this);
  }

  #endregion
}

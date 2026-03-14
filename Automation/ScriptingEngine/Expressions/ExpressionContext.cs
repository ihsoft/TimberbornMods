using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

/// <summary>Specifies the environment of the expression.</summary>
record ExpressionContext {
  public AutomationBehavior ScriptHost { get; init; }
}

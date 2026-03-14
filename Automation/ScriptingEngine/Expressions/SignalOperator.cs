// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;

namespace IgorZ.Automation.ScriptingEngine.Expressions;

sealed class SignalOperator : AbstractOperator, IValueExpr {

  const string OnUnfinishedNamePrefix = ".OnUnfinished.";

  public readonly string SignalName;
  public bool OnUnfinished => SignalName.Contains(OnUnfinishedNamePrefix);
  public readonly SignalDef SignalDef;

  /// <inheritdoc/>
  public ScriptValue.TypeEnum ValueType => SignalDef.Result.ValueType;

  /// <inheritdoc/>
  public Func<ScriptValue> ValueFn { get; }

  public static SignalOperator Create(ExpressionContext context, string name) => new(context, name);

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}('{SignalName}')";
  }

  SignalOperator(ExpressionContext context, string signalName) : base([]) {
    SignalName = signalName;
    SignalDef = ScriptingService.Instance.GetSignalDefinition(SignalName, context.ScriptHost);
    ValueFn = ScriptingService.Instance.GetSignalSource(SignalName, context.ScriptHost);
  }
}

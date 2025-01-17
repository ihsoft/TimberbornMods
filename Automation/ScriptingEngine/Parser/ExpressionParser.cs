// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Timberborn.BaseComponentSystem;
using Timberborn.Common;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ExpressionParser(ScriptingService scriptingService, BaseComponent scriptHost) {

  public sealed class Context {
    /// <summary>All the signal sources that are referenced in the parsed expression.</summary>
    /// <remarks>
    /// If there was <see cref="OnSignalChanged"/> action specified, then the sources must be disposed before cleaning
    /// up the parsed expression.
    /// </remarks>
    public readonly Dictionary<string, ITriggerSource> SignalSources = new();

    /// <summary>On successful parsing, this property will contain the parsed expression.</summary>
    public IExpression ParsedExpression { get; internal set; }

    /// <summary>On parsing error, this property will contain the error message.</summary>
    public string LastError { get; internal set; }

    /// <summary>
    /// Callback to call when any of the signal sources change their value. If set to null, then no callbacks will be
    /// registered
    /// </summary>
    //public Action OnSignalChanged { get; init; }
    public Action OnSignalChanged;

    /// <summary>
    /// Clears all internal states in the scripting engine. Must be called if the parsed expression is no more needed.
    /// </summary>
    public void Release() {
      foreach (var source in SignalSources.Values) {
        source.Dispose();
      }
      SignalSources.Clear();
    }

    ~Context() {
      if (OnSignalChanged == null || SignalSources.Count <= 0) {
        return;
      }
      DebugEx.Error("Context was not cleared before being garbage collected: {0}", ParsedExpression);
      Release();
    }
  }

  public bool Parse(string input, Context context) {
    if (context.SignalSources.Count > 0 || context.ParsedExpression != null) {
      throw new InvalidOperationException("Context is already in use");
    }
    try {
      _currentContext = context;
      _currentContext.ParsedExpression = ReadFromTokens(Tokenize(input));
      _currentContext.LastError = null;
    } catch (ScriptError e) {
      _currentContext.LastError = e.Message;
      _currentContext.ParsedExpression = null;
      _currentContext.Release();
      return false;
    } finally {
      _currentContext = null;
    }
    return true;
  }

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");
  Context _currentContext;

  internal IScriptable.ActionDef GetActionDefinition(string actionName) {
    return scriptingService.GetActionDefinition(actionName, scriptHost);
  }

  internal Action<ScriptValue[]> GetAction(string actionName) {
    return scriptingService.GetActionExecutor(actionName, scriptHost);
  }

  internal ITriggerSource GetSignalSource(string name) {
    if (!_currentContext.SignalSources.TryGetValue(name, out var source)) {
      source = scriptingService.GetTriggerSource(name, scriptHost, _currentContext.OnSignalChanged);
      _currentContext.SignalSources[name] = source;
    }
    return source;
  }

  Queue<string> Tokenize(string input) {
    if (input == null) {
      throw new ArgumentNullException(nameof(input));
    }
    var source = input.Replace("(", " ( ").Replace(")", " ) ");
    var tokens = Regex.Matches(source, "['].+?[']|[^ ]+")
        .Cast<Match>()
        .Select(m => m.Value);
    return new Queue<string>(tokens);
  }

  IExpression ReadFromTokens(Queue<string> tokens) {
    if (!tokens.Any()) {
      throw new ScriptError("Unexpected EOF while reading expression");
    }
    var token = tokens.Dequeue();
    if (token == "(") {
      if (tokens.IsEmpty()) {
        throw new ScriptError("Unexpected EOF while reading expression");
      }
      var operatorName = tokens.Dequeue();
      if (OperatorNameRegex.IsMatch(operatorName)) {
        throw new ScriptError("Bad operator name: " + operatorName);
      }
      var operands = new List<IExpression>();
      while (tokens.Peek() != ")") {
        operands.Add(ReadFromTokens(tokens));
      }
      if (operands.Count == 0) {
        throw new ScriptError("Empty operator expression");
      }
      tokens.Dequeue(); // ")"
      var result =
          BinaryOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? LogicalOperatorExpr.TryCreateFrom(operatorName, operands)
          ?? SignalOperatorExpr.TryCreateFrom(operatorName, operands, this)
          ?? ActionExpr.TryCreateFrom(operatorName, operands, this);
      if (result == null) {
        throw new ScriptError("Unknown operator: " + operatorName);
      }
      return result;
    }
    if (token == ")") {
      throw new ScriptError("Unexpected ')' while reading expression");
    }
    return ConstantValueExpr.TryCreateFrom(token) ?? new SymbolExpr { Value = token };
  }
}

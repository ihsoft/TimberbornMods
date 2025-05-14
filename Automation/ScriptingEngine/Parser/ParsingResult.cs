// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.Automation.ScriptingEngine.Parser;

readonly record struct ParsingResult {
  /// <summary>On successful parsing, this property will contain the parsed expression.</summary>
  public IExpression ParsedExpression { get; init; }

  /// <summary>On parsing error, this property will contain the error message.</summary>
  public string LastError => LastScriptError?.Message;

  /// <summary>On parsing error, this property will contain the last script error.</summary>
  public ScriptError LastScriptError { get; init; }
}

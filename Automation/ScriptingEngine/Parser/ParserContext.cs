// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using IgorZ.Automation.AutomationSystem;

namespace IgorZ.Automation.ScriptingEngine.Parser;

sealed class ParserContext {

  #region Input settings 

  /// <summary>Host object that will own this expression.</summary>
  /// <remarks>This must be set before the parser is called.</remarks>
  public AutomationBehavior ScriptHost { get; init; }

  #endregion

  #region Output data

  /// <summary>All the signal that are referenced in the parsed expression.</summary>
  public readonly HashSet<string> ReferencedSignals = [];

  /// <summary>On successful parsing, this property will contain the parsed expression.</summary>
  public IExpression ParsedExpression { get; internal set; }

  /// <summary>On parsing error, this property will contain the error message.</summary>
  public string LastError { get; internal set; }

  #endregion
}

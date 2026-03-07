using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.Settings;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class ParserFactory {

  const string ConditionMustBeBoolLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustBeBoolean";
  const string ConditionMustHaveSignalsLocKey = "IgorZ.Automation.Scripting.Editor.ConditionMustHaveSignals";
  const string ActionMustBeActionLocKey = "IgorZ.Automation.Scripting.Editor.ActionMustBeAction";

  /// <summary>Add this prefix to override system's settings and parse the expression with the Python parser.</summary>
  public const string PythonSyntaxPrefix = "#PY";

  /// <summary>Add this prefix to override system's settings and parse the expression with the Lisp parser.</summary>
  public const string LispSyntaxPrefix = "#LP";

  /// <summary>Parser for the Lisp syntax.</summary>
  public readonly ParserBase LispSyntaxParser;

  /// <summary>Parser for the Python syntax.</summary>
  public readonly ParserBase PythonSyntaxParser;

  /// <summary>The parser that was selected in the settings.</summary>
  public ParserBase DefaultParser => ScriptEditorSettings.DefaultScriptSyntax == ScriptEditorSettings.ScriptSyntax.Lisp
      ? LispSyntaxParser : PythonSyntaxParser;

  /// <summary>
  /// Parses the expression with respect to the default parser setting and the expression syntax prefix.
  /// </summary>
  /// <param name="expression">Arbitrary expression. It can contain a parser selection prefix.</param>
  /// <param name="behavior">The behavior to verify the expression against.</param>
  /// <param name="preferredParser">
  /// The parser to use if no parser tags were given in the input (see <see cref="PythonSyntaxPrefix"/> and
  /// <see cref="LispSyntaxPrefix"/>). If <c>null</c>, then the <see cref="DefaultParser"/> will be used.
  /// </param>
  public ParsingResult ParseExpression(
      string expression, AutomationBehavior behavior, ParserBase preferredParser = null) {
    if (expression.StartsWith(PythonSyntaxPrefix)) {
      return PythonSyntaxParser.Parse(expression[PythonSyntaxPrefix.Length..], behavior);
    }
    if (expression.StartsWith(LispSyntaxPrefix)) {
      return LispSyntaxParser.Parse(expression[LispSyntaxPrefix.Length..], behavior);
    }
    return (preferredParser ?? DefaultParser).Parse(expression, behavior);
  }

  /// <summary>
  /// Parses the condition expression with respect to the default parser setting and the expression syntax prefix.
  /// </summary>
  /// <remarks>
  /// Will verify if the expression is a valid condition. If not, the result will have a relevant localized error.
  /// </remarks>
  /// <param name="expression">
  /// The condition expression with an optional syntax prefix. It must be a logical operator.
  /// </param>
  /// <param name="behavior">The behavior to verify the expression against.</param>
  /// <param name="result">
  /// The parsing result. It will have a localizable parsing error if the expressions can be parsed, but is not a
  /// logical operator or doesn't contain at least one signal operator.
  /// </param>
  /// <param name="preferredParser">
  /// The parser to use if no parser tags were given in the input (see <see cref="PythonSyntaxPrefix"/> and
  /// <see cref="LispSyntaxPrefix"/>). If <c>null</c>, then the <see cref="DefaultParser"/> will be used.
  /// </param>
  /// <seealso cref="ParseExpression"/>
  /// <seealso cref="ScriptError.LocParsingError"/>
  public BooleanOperator ParseCondition(string expression, AutomationBehavior behavior, out ParsingResult result,
                                     ParserBase preferredParser = null) {
    result = ParseExpression(expression, behavior, preferredParser: preferredParser);
    if (result.ParsedExpression == null) {
      return null;
    }
    if (result.ParsedExpression is not BooleanOperator boolOperator) {
      result = new ParsingResult {
          LastScriptError = new ScriptError.LocParsingError(ConditionMustBeBoolLocKey, "Not a boolean expression"),
      };
      return null;
    }
    var hasSignals = false;
    boolOperator.VisitNodes(x => { hasSignals |= x is SignalOperator; });
    if (!hasSignals) {
      result = new ParsingResult {
          LastScriptError = new ScriptError.LocParsingError(ConditionMustHaveSignalsLocKey, "No signals in condition"),
      };
      return null;
    }
    return boolOperator;
  }

  /// <summary>
  /// Parses the action expression with respect to the default parser setting and the expression syntax prefix.
  /// </summary>
  /// <remarks>
  /// Will verify if the expression is a valid action. If not, the result will have a relevant localized error.
  /// </remarks>
  /// <param name="expression">
  /// The action expression with an optional syntax prefix. It must be an action operator.
  /// </param>
  /// <param name="behavior">The behavior to verify the expression against.</param>
  /// <param name="result">
  /// The parsing result. It will have a localizable parsing error if the expressions can be parsed, but is not an
  /// action operator.
  /// </param>
  /// <param name="preferredParser">
  /// The parser to use if no parser tags were given in the input (see <see cref="PythonSyntaxPrefix"/> and
  /// <see cref="LispSyntaxPrefix"/>). If <c>null</c>, then the <see cref="DefaultParser"/> will be used.
  /// </param>
  /// <seealso cref="ParseExpression"/>
  /// <seealso cref="ScriptError.LocParsingError"/>
  public ActionOperator ParseAction(string expression, AutomationBehavior behavior, out ParsingResult result,
                                    ParserBase preferredParser = null) {
    result = ParseExpression(expression, behavior, preferredParser: preferredParser);
    if (result.ParsedExpression == null) {
      return null;
    }
    if (result.ParsedExpression is not ActionOperator actionOperator) {
      result = new ParsingResult {
          LastScriptError = new ScriptError.LocParsingError(ActionMustBeActionLocKey, "Expressions is not an action"),
      };
      return null;
    }
    return actionOperator;
  }

  ParserFactory(LispSyntaxParser lispSyntaxParser, PythonSyntaxParser pythonSyntaxParser) {
    LispSyntaxParser = lispSyntaxParser;
    PythonSyntaxParser = pythonSyntaxParser;
  }
}

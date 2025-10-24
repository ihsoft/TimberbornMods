// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Timberborn.Common;

namespace IgorZ.Automation.ScriptingEngine.Parser;

/// <summary>Parser for the expressions in the scripting engine.</summary>
sealed class ExpressionParser : ParserBase {

  #region ParserBase implementation

  /// <inheritdoc/>
  protected override IExpression ProcessString(string input) {
    var tokens = Tokenize(input);
    var result = ReadFromTokens(tokens);
    if (tokens.Any()) {
      throw new ScriptError.ParsingError("Unexpected token at the end of the expression: " + tokens.Peek());
    }
    return result;
  }

  #endregion

  #region Implementation

  static readonly Regex OperatorNameRegex = new(@"^\[a-zA-Z]+$");

  static Queue<string> Tokenize(string input) {
    if (input == null) {
      throw new ArgumentNullException(nameof(input));
    }
    var currentPos = 0;
    var tokens = new Queue<string>();
    while (currentPos < input.Length) {
      // Skip the leading spaces.
      if (input[currentPos] == ' ') {
        while (currentPos < input.Length && input[currentPos] == ' ') {
          currentPos++;
        }
        continue;
      }

      // Capture the string literal.
      if (input[currentPos] == '\'') {
        var literalEndPos = input.IndexOf('\'', currentPos + 1);
        if (literalEndPos == -1) {
          throw new ScriptError.ParsingError("Unmatched quote");
        }
        tokens.Enqueue(input.Substring(currentPos, literalEndPos - currentPos + 1));
        currentPos = literalEndPos + 1;
        continue;
      }

      // Capture the statement separator.
      if (input[currentPos] == '(' || input[currentPos] == ')') {
        tokens.Enqueue(input.Substring(currentPos, 1));
        currentPos++;
        continue;
      }
      
      // Capture the token.
      var tokenEndPos = currentPos + 1;
      while (tokenEndPos < input.Length && input[tokenEndPos] != ' ' && input[tokenEndPos] != '(' && input[tokenEndPos] != ')') {
        tokenEndPos++;
      }
      tokens.Enqueue(input.Substring(currentPos, tokenEndPos - currentPos));
      currentPos = tokenEndPos;
    }
    return tokens;
  }

  static void CheckHasMoreTokens(Queue<string> tokens) {
    if (tokens.IsEmpty()) {
      throw new ScriptError.ParsingError("Unexpected EOF while reading expression");
    }
  }

  IExpression ReadFromTokens(Queue<string> tokens) {
    if (!tokens.Any()) {
      throw new ScriptError.ParsingError("Unexpected EOF while reading expression");
    }
    var token = tokens.Dequeue();
    if (token != "(") {
      var constantValue = ConstantValueExpr.TryCreateFrom(token);
      if (constantValue != null) {
        return constantValue;
      }
      return new SymbolExpr(token);
    }
    CheckHasMoreTokens(tokens);
    var operatorName = tokens.Dequeue();
    if (OperatorNameRegex.IsMatch(operatorName)) {
      throw new ScriptError.ParsingError("Bad operator name: " + operatorName);
    }
    CheckHasMoreTokens(tokens);
    var operands = new List<IExpression>();
    while (tokens.Peek() != ")") {
      operands.Add(ReadFromTokens(tokens));
      CheckHasMoreTokens(tokens);
    }
    if (operands.Count == 0) {
      throw new ScriptError.ParsingError("Empty operator expression");
    }
    tokens.Dequeue(); // ")"

    // The sequence below should be ordered by the frequency of the usage. The operators that are more likely to be
    // used in the game should come first.
    var result =
        BinaryOperator.TryCreateFrom(CurrentContext, operatorName, operands)
        ?? SignalOperator.TryCreateFrom(CurrentContext, operatorName, operands)
        ?? ActionOperator.TryCreateFrom(CurrentContext, operatorName, operands)
        ?? LogicalOperator.TryCreateFrom(operatorName, operands)
        ?? MathOperator.TryCreateFrom(operatorName, operands)
        ?? GetPropertyOperator.TryCreateFrom(CurrentContext, operatorName, operands)
        ?? HasComponentOperator.TryCreateFrom(CurrentContext, operatorName, operands)
        ?? ConcatOperator.TryCreateFrom(CurrentContext, operatorName, operands);
    if (result == null) {
      throw new ScriptError.ParsingError("Unknown operator: " + operatorName);
    }
    return result;
  }

  #endregion
}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using UnityEngine;
using Token = IgorZ.Automation.ScriptingEngine.Parser.TokenizerBase.Token;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class PythonSyntaxParser : ParserBase {

  #region ParserBase implementation

  static readonly TokenizerBase Tokenizer = new PythonSyntaxTokenizer();

  protected override IExpression ProcessString(string input) {
    var tokens = Tokenizer.Tokenize(input);
    var result = ParseExpressionInternal(-1, tokens);
    if (tokens.Count > 0) {
      throw new InvalidOperationException("Unexpected token at the end of the expression: " + tokens.Peek());
    }
    return result;
  }

  #endregion

  #region API

  /// <inheritdoc/>
  public override string Decompile(IExpression expression) {
    return DecompileInternal(expression);
  }

  #endregion

  #region Implementation

  const string EqOperator = "==";
  const string NeOperator = "!=";
  const string LtOperator = "<";
  const string LeOperator = "<=";
  const string GtOperator = ">";
  const string GeOperator = ">=";
  const string AndOperator = "and";
  const string OrOperator = "or";
  const string NotOperator = "not";
  const string AddOperator = "+";
  const string SubOperator = "-";
  const string MulOperator = "*";
  const string DivOperator = "/";
  const string ModOperator = "%";
  const string MinFunc = "min";
  const string MaxFunc = "max";
  const string RoundFunc = "round";
  const string GetValueFunc = "getvalue";
  const string GetElementFunc = "getelement";
  const string GetLenFunc = "getlen";
  const string ConcatFunc = "concat";

  static readonly Dictionary<string, int> InfixOperatorsPrecedence = new() {
      { OrOperator, 0 },
      { AndOperator, 1 },
      { NotOperator, 2 },
      { EqOperator, 3 },
      { NeOperator, 3 },
      { LtOperator, 4 },
      { LeOperator, 4 },
      { GtOperator, 4 },
      { GeOperator, 4 },
      { AddOperator, 5 },
      { SubOperator, 5 },
      { ModOperator, 7 },
      { DivOperator, 7 },
      { MulOperator, 7 },
  };

  static readonly string[] GroupTerminator = [ ")" ];
  static readonly string[] ArgumentsTerminators = [ ")", "," ];
  static readonly Regex IsGoodFloatValueRegex = new(@"^-?\d+(\.[0-9]{1,2}[0]*)?$");

  IExpression ParseExpressionInternal(int parentPrecedence, Queue<Token> tokens) {
    var left = ConsumeValueOperand(tokens);
    while (tokens.Count > 0) {
      var opName = tokens.Peek();
      if (!InfixOperatorsPrecedence.TryGetValue(opName.Value, out var precedence)) {
        throw new ScriptError.ParsingError(opName, "Expected operator");
      }
      if (parentPrecedence >= precedence) {
        return left;
      }
      tokens.Dequeue(); // Consume operator.
      var operands = new List<IExpression> { left, ParseExpressionInternal(precedence, tokens) };
      left = opName.Value switch {
          OrOperator => LogicalOperator.CreateOr(CollapseLogicalOperators(LogicalOperator.OpType.Or, operands)),
          AndOperator => LogicalOperator.CreateAnd(CollapseLogicalOperators(LogicalOperator.OpType.And, operands)),
          EqOperator => BinaryOperator.CreateEq(CurrentContext, operands),
          NeOperator => BinaryOperator.CreateNe(CurrentContext, operands),
          LtOperator => BinaryOperator.CreateLt(CurrentContext, operands),
          LeOperator => BinaryOperator.CreateLe(CurrentContext, operands),
          GtOperator => BinaryOperator.CreateGt(CurrentContext, operands),
          GeOperator => BinaryOperator.CreateGe(CurrentContext, operands),
          AddOperator => MathOperator.CreateAdd(CollapseMathOperators(operands)),
          SubOperator => MathOperator.CreateSubtract(operands),
          DivOperator => MathOperator.CreateDivide(operands),
          MulOperator => MathOperator.CreateMultiply(operands),
          ModOperator => MathOperator.CreateModulus(operands),
          _ => throw new ScriptError.ParsingError(opName, "Expected operator"),
      };
    }
    return left;
  }

  static List<IExpression> CollapseMathOperators(List<IExpression> operands) {
    while (operands[0] is MathOperator { OperatorType: MathOperator.OpType.Add } op) {
      operands.RemoveAt(0);
      operands.InsertRange(0, op.Operands);
    }
    return operands;
  }

  static List<IExpression> CollapseLogicalOperators(LogicalOperator.OpType opType, List<IExpression> operands) {
    while (operands[0] is LogicalOperator op && op.OperatorType == opType) {
      operands.RemoveAt(0);
      operands.InsertRange(0, op.Operands);
    }
    return operands;
  }

  /// <summary>It consumes a value operand and throws if the operand is not a value.</summary>
  /// <remarks>Functions are value operators. A group that starts with '(' is expected to be it as well.</remarks>
  IExpression ConsumeValueOperand(Queue<Token> tokens) {
    var token = PopToken(tokens);

    // Unary operators.
    if (token is { TokenType: Token.Type.Keyword, Value: NotOperator }) {
      return LogicalOperator.CreateNot(ParseExpressionInternal(InfixOperatorsPrecedence[NotOperator], tokens));
    }
    if (token is { TokenType: Token.Type.Keyword, Value: SubOperator }) {
      var operand = ConsumeValueOperand(tokens);
      return operand is ConstantValueExpr constantValueExpr
          ? ConstantValueExpr.CreateFromValue(-constantValueExpr.ValueFn())
          : MathOperator.CreateNegate(operand);
    }

    // Expression group: ( ... )
    if (IsGroupOpenToken(token)) {
      if (IsGroupCloseToken(PreviewToken(tokens))) {
        throw new ScriptError.ParsingError(token, "Expected value or operator");
      }
      return ConsumeSequence(tokens, GroupTerminator, out _);
    }

    // Signals ("variables" in Python syntax) and actions ("functions" in Python):
    // Floodgate.Height, Floodgate.SetHeight(12)
    if (token.TokenType == Token.Type.Identifier) {
      if (!token.Value.Contains(".")) {
        throw new ScriptError.ParsingError(token, "Unknown function");
      }
      return tokens.Count == 0 || !IsGroupOpenToken(tokens.Peek())
          ? SignalOperator.Create(CurrentContext, token.Value)
          : ActionOperator.Create(CurrentContext, token.Value, ConsumeArgumentsGroup(tokens));
    }

    // Constant values.
    if (token.TokenType == Token.Type.StringLiteral) {
      return ConstantValueExpr.CreateStringLiteral(token.Value);
    }
    if (token.TokenType == Token.Type.NumericValue) {
      if (!float.TryParse(token.Value, out var value)) {
        throw new ScriptError.ParsingError(token, "Not a valid float number");
      }
      return IsGoodFloatValueRegex.IsMatch(token.Value)
          ? ConstantValueExpr.CreateFromValue(ScriptValue.FromFloat(value))
          : throw new ScriptError.ParsingError(token, "Only up to 2 digits after the decimal point are allowed.");
    }

    if (token.TokenType != Token.Type.Keyword) {
      throw new Exception($"Unexpected token: {token}");
    }

    // Functions.
    var arguments = ConsumeArgumentsGroup(tokens);
    if (token.Value == GetValueFunc) {
      AssertNumberOfOperandsExact(token, arguments, 1);
      return GetPropertyFunction.CreateGetOrdinary(CurrentContext, UnwrapStringLiteralExpr(token, arguments, 0));
    }
    if (token.Value == GetElementFunc) {
      AssertNumberOfOperandsExact(token, arguments, 2);
      return GetPropertyFunction.CreateGetCollectionElement(
          CurrentContext, UnwrapStringLiteralExpr(token, arguments, 0), arguments[1]);
    }
    if (token.Value == GetLenFunc) {
      AssertNumberOfOperandsExact(token, arguments, 1);
      return GetPropertyFunction.CreateGetCollectionLength(
          CurrentContext, UnwrapStringLiteralExpr(token, arguments, 0));
    }
    return token.Value switch {
        MinFunc => MathOperator.CreateMin(arguments),
        MaxFunc => MathOperator.CreateMax(arguments),
        RoundFunc => MathOperator.CreateRound(arguments),
        ConcatFunc => ConcatOperator.Create(arguments),
        _ => throw new ScriptError.ParsingError(token, "Unknown function"),
    };
  }

  IExpression ConsumeSequence(Queue<Token> tokens, string[] terminators, out Token terminator) {
    var sequence = new Queue<Token>();
    var parenCount = 0;
    while (true) {
      var token = PopToken(tokens);
      if (parenCount == 0 && token.TokenType == Token.Type.StopSymbol && terminators.Contains(token.Value)) {
        terminator = token;
        break;
      } 
      if (IsGroupOpenToken(token)) {
        parenCount++;
      } else if (IsGroupCloseToken(token)) {
        if (parenCount == 0) {
          throw new ScriptError.ParsingError("Unexpected EOF while reading sequence");
        }
        parenCount--;
      }
      sequence.Enqueue(token);
    }
    return ParseExpressionInternal(-1, sequence);
  }

  static Token PopToken(Queue<Token> tokens) {
    return tokens.Count == 0
        ? throw new ScriptError.ParsingError("Unexpected EOF while reading expression")
        : tokens.Dequeue();
  }

  static Token PreviewToken(Queue<Token> tokens) {
    return tokens.Count == 0
        ? throw new ScriptError.ParsingError("Unexpected EOF while reading expression")
        : tokens.Peek();
  }

  IList<IExpression> ConsumeArgumentsGroup(Queue<Token> tokens) {
    var openToken = PopToken(tokens);
    if (!IsGroupOpenToken(openToken)) {
      throw new ScriptError.ParsingError(openToken, "Expected opening parenthesis");
    }
    if (IsGroupCloseToken(PreviewToken(tokens))) {
      PopToken(tokens);  // Consume ")"
      return [];
    }
    var arguments = new List<IExpression>();
    Token terminator;
    do {
      arguments.Add(ConsumeSequence(tokens, ArgumentsTerminators, out terminator));
    } while (!IsGroupCloseToken(terminator));
    return arguments;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool IsGroupOpenToken(Token token) {
    return token is { TokenType: Token.Type.StopSymbol, Value: "(" };
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool IsGroupCloseToken(Token token) {
    return token is { TokenType: Token.Type.StopSymbol, Value: ")" };
  }

  static string DecompileInternal(IExpression expression) {
    return expression switch {
        AbstractFunction abstractFunction => DecompileFunction(abstractFunction),
        AbstractOperator abstractOperator => DecompileOperator(abstractOperator),
        ConstantValueExpr constExpr => constExpr.ValueType switch {
            ScriptValue.TypeEnum.String => Tokenizer.EscapeString(constExpr.ValueFn().AsString),
            ScriptValue.TypeEnum.Number => constExpr.ValueFn().AsFloat.ToString("0.##"),
            _ => throw new InvalidOperationException($"Unsupported value type: {constExpr.ValueType}"),
        },
        _ => throw new InvalidOperationException($"Unsupported expression type: {expression}"),
    };
  }

  static string DecompileOperator(AbstractOperator expression) {
    // Signals. They are technically variables: My.variable.name1
    if (expression is SignalOperator signalOperator) {
      return signalOperator.SignalName;
    }

    // Functions: func(a, b, c).
    var funcName = expression switch {
        MathOperator mathOperator => mathOperator.OperatorType switch {
            MathOperator.OpType.Min => MinFunc,
            MathOperator.OpType.Max => MaxFunc,
            MathOperator.OpType.Round => RoundFunc,
            _ => null,
        },
        ConcatOperator => ConcatFunc,
        ActionOperator actionOperator => actionOperator.ActionName,
        _ => null,
    };
    if (funcName != null) {
      var args = string.Join(", ", expression.Operands.Select(DecompileInternal));
      return $"{funcName}({args})";
    }

    // Unary operators.
    if (expression is LogicalOperator { OperatorType: LogicalOperator.OpType.Not }) {
      var value = DecompileLeft(expression.Operands[0], expression);
      return $"{NotOperator} {value}";
    }
    if (expression is MathOperator { OperatorType: MathOperator.OpType.Negate }) {
      var value = DecompileLeft(expression.Operands[0], expression);
      return $"{SubOperator}{value}";
    }

    // Binary operators: a + b
    var operands = expression.GetReducedOperands();
    var opName = expression switch {
        BinaryOperator binaryOperator => binaryOperator.OperatorType switch {
            BinaryOperator.OpType.Equal => EqOperator,
            BinaryOperator.OpType.NotEqual => NeOperator,
            BinaryOperator.OpType.LessThan => LtOperator,
            BinaryOperator.OpType.LessThanOrEqual => LeOperator,
            BinaryOperator.OpType.GreaterThan => GtOperator,
            BinaryOperator.OpType.GreaterThanOrEqual => GeOperator,
            _ => throw new InvalidOperationException($"Unsupported operator: {binaryOperator.OperatorType}"),
        },
        MathOperator mathOperator => mathOperator.OperatorType switch {
            MathOperator.OpType.Add => AddOperator,
            MathOperator.OpType.Subtract => SubOperator,
            MathOperator.OpType.Multiply => MulOperator,
            MathOperator.OpType.Divide => DivOperator,
            MathOperator.OpType.Modulus => ModOperator,
            _ => throw new InvalidOperationException($"Unsupported operator: {mathOperator.OperatorType}"),
        },
        LogicalOperator logicalOperator => logicalOperator.OperatorType switch {
            LogicalOperator.OpType.And => AndOperator,
            LogicalOperator.OpType.Or => OrOperator,
            LogicalOperator.OpType.Not => throw new InvalidOperationException("Not expected"),
            _ => throw new InvalidOperationException($"Unsupported operator: {logicalOperator.OperatorType}"),
        },
        _ => throw new InvalidOperationException($"Unexpected expression type: {expression}"),
    };
    var leftValue = DecompileLeft(operands[0], expression);
    // Add and Multiply operators are not strictly left-associative. Avoid unneeded parenthesis.
    var rightValue = opName is AddOperator or MulOperator
        ? DecompileLeft(operands[1], expression)
        : DecompileRight(operands[1], expression);
    return $"{leftValue} {opName} {rightValue}";
  }

  static string DecompileFunction(AbstractFunction function) {
    if (function is GetPropertyFunction getPropertyFunction) {
      var propertyName = getPropertyFunction.PropertyFullName;
      return getPropertyFunction.FunctionName switch {
          GetPropertyFunction.FuncName.Value => $"{GetValueFunc}('{propertyName}')",
          GetPropertyFunction.FuncName.Element =>
              $"{GetElementFunc}('{propertyName}', {DecompileInternal(getPropertyFunction.IndexExpr)})",
          GetPropertyFunction.FuncName.Length => $"{GetLenFunc}('{propertyName}')",
          _ => throw new InvalidOperationException($"Unexpected GetPropertyFunction: {propertyName}"),
      };
    }
    throw new InvalidOperationException($"Unsupported expression type: {function}");
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static string DecompileLeft(IExpression operand, IExpression parent) {
    var value = DecompileInternal(operand);
    return InfixExpressionUtil.ResolvePrecedence(parent) > InfixExpressionUtil.ResolvePrecedence(operand)
        ? $"({value})"
        : value;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static string DecompileRight(IExpression operand, IExpression parent) {
    var value = DecompileInternal(operand);
    return InfixExpressionUtil.ResolvePrecedence(parent) >= InfixExpressionUtil.ResolvePrecedence(operand)
        ? $"({value})"
        : value;
  }

  #endregion

  #region Tokenizer

  class PythonSyntaxTokenizer : TokenizerBase {
    /// <inheritdoc/>
    protected override string StringQuotes => "'\"";

    /// <inheritdoc/>
    protected override string StopSymbols => "(),+-/*%=<>";

    /// <inheritdoc/>
    protected override HashSet<string> Keywords => [
        // Logical operators.
        AndOperator, OrOperator, NotOperator,
        // Math functions.
        MinFunc, MaxFunc, RoundFunc,
        // Get property functions.
        GetValueFunc, GetElementFunc, GetLenFunc,
        // Concat operator.
        ConcatFunc,
    ];

    /// <inheritdoc/>
    protected override string[] StopSymbolsKeywords => [
        EqOperator, NeOperator, LtOperator, LeOperator, GtOperator, GeOperator, // stops: =<>
        AddOperator, SubOperator, DivOperator, MulOperator, ModOperator, // stops: +-/*%
    ];
  }

  #endregion
}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Reflection;
using IgorZ.Automation.ScriptingEngine.Values;

namespace IgorZ.Automation.ScriptingEngine.Nodes;

/// <summary>Expression node that represents a function call.</summary>
/// <remarks>
/// The function must be declared in a class that implements <see cref="IScriptableInstance"/> and be annotated with
/// <see cref="IScriptableInstance.ScriptFunctionAttribute"/> attribute. Function can accept and return basic C# types
/// or <see cref="IExpressionValue"/>. Methods are treated as functions returning <c>null</c>.
/// </remarks>
class FunctionNode : ExpressionNode {

  #region API

  /// <summary>Name of the method to call. It is a real name of the method in the C# class.</summary>
  public string MethodName { get; }

  #endregion

  #region ExpressionNode implementation

  /// <inheritdoc />
  public override IExpressionValue Eval() {
    var invokeArgs = new List<object>();
    var reflectedArgs = _methodInfo.GetParameters();
    for (var i = 0; i < _argNodes.Length; i++) {
      var value = _argNodes[i].Eval();
      var reflectedArgType = reflectedArgs[i].ParameterType;
      if (reflectedArgType == typeof(IExpressionValue)) {
        invokeArgs.Add(value);
      } else if (reflectedArgType == typeof(string)) {
        invokeArgs.Add(value.AsString());
      } else if (reflectedArgType == typeof(int)) {
        invokeArgs.Add(value.AsNumber() / 100);
      } else if (reflectedArgType == typeof(float)) {
        invokeArgs.Add(value.AsNumber() / 100f);
      } else if (reflectedArgType == typeof(bool)) {
        invokeArgs.Add(value.AsBool());
      } else {
        throw new InvalidOperationException("Unsupported argument type: " + reflectedArgType);
      }
    }

    var result = _methodInfo.Invoke(_instance, invokeArgs.ToArray());
    if (result == null) {
      return null;
    }
    return result switch {
        string strResult => StringValue.FromLiteral(strResult),
        int intResult => NumberValue.FromInt(intResult),
        float floatResult => NumberValue.FromFloat(floatResult),
        bool boolResult => BoolValue.FromBool(boolResult),
        IExpressionValue valueResult => valueResult,
        _ => throw new InvalidOperationException("Unsupported return type: " + result.GetType())
    };
  }

  #endregion

  #region Implementation

  readonly ExpressionNode[] _argNodes;
  readonly MethodInfo _methodInfo;
  readonly IScriptableInstance _instance;

  //FIXME: check here or in parser if there is a return value
  public FunctionNode(IScriptableInstance instance, string methodName, ExpressionNode[] argNodes) {
    MethodName = methodName;
    _instance = instance;
    _argNodes = argNodes;

    _methodInfo = instance.GetType().GetMethod(methodName);
    var attribute = _methodInfo?.GetCustomAttribute<IScriptableInstance.ScriptFunctionAttribute>();
    if (_methodInfo == null || attribute == null) {
      throw new ScriptError($"Method {methodName} not found");
    }

    var numArgs = _methodInfo.GetParameters().Length;
    if (numArgs != argNodes.Length) {
      throw new ScriptError($"Expected {numArgs} arguments, got {argNodes.Length}");
    }
    //FIXME: check if all arguyments types are correct
    //FIXME: check if no variable parameters. 
  }

  #endregion
}

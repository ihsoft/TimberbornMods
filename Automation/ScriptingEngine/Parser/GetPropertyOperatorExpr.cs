// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.Parser;

class GetPropertyOperatorExpr : AbstractOperandExpr, IValueExpr {
  public override string Describe() {
    throw new NotImplementedException();
  }
  public ScriptValue.TypeEnum ValueType { get; set; }
  public Func<ScriptValue> ValueFn { get; set; }

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name switch {
        "getstr" => new GetPropertyOperatorExpr(ScriptValue.TypeEnum.String, context, name, operands),
        "getnum" => new GetPropertyOperatorExpr(ScriptValue.TypeEnum.Number, context, name, operands),
        _ => null,
    };
  }

  GetPropertyOperatorExpr(ScriptValue.TypeEnum valueType, ExpressionParser.Context context,
                          string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsExact(1);
    if (Operands[0] is not SymbolExpr symbol) {
      throw new ScriptError("Bad property name: " + Operands[0]);
    }
    var parts = symbol.Value.Split('.');
    if (parts.Length != 2) {
      throw new ScriptError("Bad property name: " + Operands[0]);
    }
    var component = GetComponentByName(context.ScriptHost.GameObjectFast, parts[0]);
    if (!component) {
      throw new ScriptError($"{DebugEx.ObjectToString(context.ScriptHost)} doesn't have component {parts[0]}");
    }
    var property = component.GetType().GetProperty(parts[1]);
    if (property == null) {
      throw new ScriptError($"{DebugEx.ObjectToString(component)} doesn't have property {parts[1]}");
    }
    var value = property.GetValue(component);
    if (valueType == ScriptValue.TypeEnum.Number) {
      if (value is int intVal){
        ValueFn = () => ScriptValue.FromInt(intVal);
      } else if (value is float floatVal) {
        ValueFn = () => ScriptValue.FromFloat(floatVal);
      } else if (value is bool boolVal) {
        ValueFn = () => ScriptValue.Of(boolVal ? 100 : 0);
      } else {
        throw new ScriptError($"Property {symbol.Value} is of incompatible type: {value.GetType()}");
      }
    } else {
      if (value is not string strVal) {
        throw new ScriptError($"Property {symbol.Value} is of incompatible type: {value.GetType()}");
      }
      ValueFn = () => ScriptValue.Of(strVal);
    }
  }

  static BaseComponent GetComponentByName(GameObject obj, string name) {
    return obj.GetComponent(name) as BaseComponent;
  }
}

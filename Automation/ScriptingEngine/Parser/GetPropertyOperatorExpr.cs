// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
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
    AsserNumberOfOperandsRange(1, -1);
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
    var listVal = GetAsList(value);
    if (listVal != null) {
      if (operands.Count == 1) {
        if (valueType != ScriptValue.TypeEnum.Number) {
          throw new ScriptError("Number type required to return list count");
        }
        value = listVal.Count;
      } else {
        AsserNumberOfOperandsExact(2);
        if (Operands[1] is not IValueExpr indexExpr) {
          throw new ScriptError("Second operand must be a value, found: " + Operands[1]);
        }
        var index = indexExpr.ValueFn().AsInt;
        if (index < 0 || index >= operands.Count) {
          throw new ScriptError($"Index {index} is out of range: [{0}; {listVal.Count})");
        }
        value = listVal[indexExpr.ValueFn().AsInt];
      }
    }
    ValueFn = valueType switch {
        ScriptValue.TypeEnum.Number => value switch {
            int intVal => () => ScriptValue.FromInt(intVal),
            float floatVal => () => ScriptValue.FromFloat(floatVal),
            bool boolVal => () => ScriptValue.Of(boolVal ? 100 : 0),
            _ => throw new ScriptError($"Property {symbol.Value} is of incompatible type: {value.GetType()}"),
        },
        ScriptValue.TypeEnum.String => value switch {
            string strVal => () => ScriptValue.Of(strVal),
            _ => throw new ScriptError($"Property {symbol.Value} is of incompatible type: {value.GetType()}"),
        },
        _ => throw new ScriptError($"Property {symbol.Value} is of incompatible type: {value.GetType()}"),
    };
  }

  static BaseComponent GetComponentByName(GameObject obj, string name) {
    if (name == "Inventory") {
      // Special case: the buildings can have more than one inventory. 
      var baseComponent = obj.GetComponent<BaseComponent>();  // Any component is good.
      return InventoryScriptableComponent.GetInventory(baseComponent);
    }
    var components = obj.GetComponents<BaseComponent>();
    return components.FirstOrDefault(x => x.enabled && x.GetType().Name == name);
  }

  static IList GetAsList(object value) {
    var getEnumeratorMethod = value.GetType().GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.Instance);
    if (getEnumeratorMethod == null) {
      return null;
    }
    var enumerator = getEnumeratorMethod.Invoke(value, null);
    if (enumerator is not IEnumerator enumeratorObj) {
      return null;
    }
    var list = new List<object>();
    while (enumeratorObj.MoveNext()) {
      list.Add(enumeratorObj.Current);
    }
    list.Sort();
    return list;
  }
}

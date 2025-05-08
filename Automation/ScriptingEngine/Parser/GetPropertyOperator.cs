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

namespace IgorZ.Automation.ScriptingEngine.Parser;

class GetPropertyOperator : AbstractOperator, IValueExpr {
  public override string Describe() {
    throw new NotImplementedException();
  }
  public ScriptValue.TypeEnum ValueType { get; set; }
  public Func<ScriptValue> ValueFn { get; set; }

  public static IExpression TryCreateFrom(ExpressionParser.Context context, string name, IList<IExpression> operands) {
    return name switch {
        "getstr" => new GetPropertyOperator(ScriptValue.TypeEnum.String, context, name, operands),
        "getnum" => new GetPropertyOperator(ScriptValue.TypeEnum.Number, context, name, operands),
        _ => null,
    };
  }

  GetPropertyOperator(ScriptValue.TypeEnum valueType, ExpressionParser.Context context,
                          string name, IList<IExpression> operands)
      : base(name, operands) {
    AsserNumberOfOperandsRange(1, -1);
    if (Operands[0] is not SymbolExpr symbol) {
      throw new ScriptError.ParsingError("Expected a symbol: " + Operands[0]);
    }
    var parts = symbol.Value.Split('.');
    if (parts.Length != 2) {
      throw new ScriptError.ParsingError("Bad property name: " + Operands[0]);
    }

    Type propType;
    var propValueFn = context.ScriptingService.GetPropertySource(symbol.Value, context.ScriptHost);
    if (propValueFn != null) {
      propType = propValueFn().GetType();
    } else {
      var componentName = parts[0];
      var component = GetComponentByName(context.ScriptHost, componentName);
      if (!component) {
        throw new ScriptError.BadStateError(context.ScriptHost, $"Component {componentName} not found");
      }
      var propertyName = parts[1];
      var property = component.GetType().GetProperty(propertyName);
      if (property == null) {
        throw new ScriptError.ParsingError($"Property {propertyName} not found on component {componentName}");
      }
      propType = property.PropertyType;
      propValueFn = () => property.GetValue(component)
          ?? (propType == typeof(string) ? "NULL" : Activator.CreateInstance(propType));
    }

    var listObject = propValueFn();
    var listVal = GetAsList(listObject);
    if (listVal != null) {
      if (operands.Count == 1) {
        if (valueType != ScriptValue.TypeEnum.Number) {
          throw new ScriptError.ParsingError("The list type counter cannot be accessed as string");
        }
        propValueFn = () => GetAsList(listObject).Count;
      } else {
        AsserNumberOfOperandsExact(2);
        if (Operands[1] is not IValueExpr { ValueType: ScriptValue.TypeEnum.Number } indexExpr) {
          throw new ScriptError.ParsingError("Second operand must be a numeric value, found: " + Operands[1]);
        }
        propValueFn = () => {
          var list = GetAsList(listObject);
          var index = indexExpr.ValueFn().AsInt;
          if (index < 0 || index >= operands.Count) {
            throw new ScriptError.RuntimeError($"Index {index} is out of range: [{0}; {list.Count})");
          }
          return list[index];
        };
      }
    }

    ValueFn = valueType switch {
        ScriptValue.TypeEnum.Number when propType == typeof(int) => () => ScriptValue.FromInt((int)propValueFn()),
        ScriptValue.TypeEnum.Number when propType == typeof(float) => () => ScriptValue.FromFloat((float)propValueFn()),
        ScriptValue.TypeEnum.Number when propType == typeof(bool) => () => ScriptValue.FromBool((bool)propValueFn()),
        ScriptValue.TypeEnum.Number => throw new ScriptError.ParsingError(
            $"Property {symbol.Value} is of incompatible type: {propType}"),
        ScriptValue.TypeEnum.String when propType == typeof(string) => () => ScriptValue.Of((string)propValueFn()),
        ScriptValue.TypeEnum.String => throw new ScriptError.ParsingError(
            $"Property {symbol.Value} is of incompatible type: {propType}"),
        _ => throw new InvalidOperationException("Unsupported value type: " + valueType)
    };
  }

  internal static BaseComponent GetComponentByName(BaseComponent baseComponent, string name) {
    if (name == "Inventory") {
      // Special case: the buildings can have more than one inventory. 
      return InventoryScriptableComponent.GetInventory(baseComponent, throwIfNotFound: false);
    }
    var components = baseComponent.AllComponents.OfType<BaseComponent>();
    return components.FirstOrDefault(x => x.enabled && x.GetType().Name == name);
  }

  /// <summary>Converts an object to a list. The object must implement the GetEnumerator method.</summary>
  static IList GetAsList(object value) {
    if (value is string) {
      return null;  // Strings are enumerable, but they aren't lists.
    }
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

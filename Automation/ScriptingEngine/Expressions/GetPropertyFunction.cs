// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using Timberborn.BaseComponentSystem;
namespace IgorZ.Automation.ScriptingEngine.Expressions;

sealed class GetPropertyFunction : AbstractFunction, IValueExpr {

  public enum FuncName {
    Value,
    Element,
    Length,
  }

  public readonly FuncName FunctionName;
  public readonly string PropertyFullName;
  public readonly IValueExpr IndexExpr;
  public readonly BaseComponent Component;

  /// <inheritdoc/>
  public void VisitNodes(Action<IExpression> visitorFn) {
    visitorFn(this);
    if (IndexExpr != null) {
      visitorFn(IndexExpr);
    }
  }

  /// <inheritdoc/>
  public ScriptValue.TypeEnum ValueType { get; init; }
  /// <inheritdoc/>
  public Func<ScriptValue> ValueFn { get; init; }

  public static GetPropertyFunction CreateGetOrdinary(ExpressionContext context, string fullPropertyName) {
    return new GetPropertyFunction(FuncName.Value, context, fullPropertyName, null);
  }

  public static GetPropertyFunction CreateGetCollectionElement(
      ExpressionContext context, string fullPropertyName, IExpression indexExpr) {
    return new GetPropertyFunction(FuncName.Element, context, fullPropertyName, indexExpr);
  }

  public static GetPropertyFunction CreateGetCollectionLength(ExpressionContext context, string fullPropertyName) {
    return new GetPropertyFunction(FuncName.Length, context, fullPropertyName, null);
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"{GetType().Name}({FunctionName})";
  }

  GetPropertyFunction(FuncName funcName, ExpressionContext context, string propertyFullName, IExpression indexExpr) {
    FunctionName = funcName;
    PropertyFullName = propertyFullName;
    if (funcName == FuncName.Element) {
      //FXIME; if expression, then can throw.
      if (indexExpr is not IValueExpr valueExpr) {
        throw new ScriptError.ParsingError($"Element index must be value expression");
      }
      IndexExpr = valueExpr;
    }
    (Component, var propertyGetterInfo) = GetPropertyGetter(propertyFullName, context.ScriptHost);
    switch (funcName) {
      case FuncName.Value:
        (ValueFn, ValueType) = MakeOrdinaryGetter(
            propertyGetterInfo.ReturnType, () => propertyGetterInfo.Invoke(Component, []));
        break;
      case FuncName.Element:
        (ValueFn, ValueType) = MakeCollectionElementGetter(propertyGetterInfo);
        break;
      case FuncName.Length:
        (ValueFn, ValueType) = MakeCollectionCountGetter(propertyGetterInfo);
        break;
      default:
        throw new InvalidOperationException($"Unexpected function: {funcName}");
    }
  }

  // Static only to help the compatibility code to work.
  static (BaseComponent component, MethodInfo getterInfo) GetPropertyGetter(
      string fullPropertyName, AutomationBehavior scriptHost) {
    var parts = fullPropertyName.Split('.');
    if (parts.Length != 2) {
      throw new ScriptError.ParsingError($"Bad property name: {fullPropertyName}");
    }
    var componentName = parts[0];
    var component = GetComponentByName(scriptHost, componentName);
    if (!component) {
      throw new ScriptError.BadStateError(scriptHost, $"Component {componentName} not found");
    }
    var propertyName = parts[1];
    var propertyGetterInfo = component.GetType().GetProperty(propertyName)?.GetMethod;
    if (propertyGetterInfo == null) {
      throw new ScriptError.ParsingError($"Property not found: '{fullPropertyName}'");
    }
    return (component, propertyGetterInfo);
  }

  (Func<ScriptValue> valueFn, ScriptValue.TypeEnum valueType) MakeOrdinaryGetter(Type valueType, Func<object> valueFn) {
    if (valueType == typeof(string)) {
      return (() => ScriptValue.FromString((string)valueFn()), ScriptValue.TypeEnum.String);
    }
    if (valueType == typeof(int)) {
      return (() => ScriptValue.FromInt((int)valueFn()), ScriptValue.TypeEnum.Number);
    }
    if (valueType == typeof(float)) {
      return (() => ScriptValue.FromFloat((float)valueFn()), ScriptValue.TypeEnum.Number);
    }
    if (valueType == typeof(bool)) {
      return (() => ScriptValue.FromBool((bool)valueFn()), ScriptValue.TypeEnum.Number);
    }
    throw new ScriptError.ParsingError($"{PropertyFullName} property has unsupported type {valueType}");
  }

  (Func<ScriptValue> valueFn, ScriptValue.TypeEnum valueType) MakeCollectionElementGetter(
      MethodInfo propertyGetterInfo) {
    return MakeOrdinaryGetter(GetElementType(propertyGetterInfo), ValueFn);

    object ValueFn() {
      var index = IndexExpr.ValueFn().AsInt;
      var list = ToList(propertyGetterInfo.Invoke(Component, []));
      return index < list.Count
          ? list[index]
          : throw new ScriptError.ValueOutOfRange($"Index {index} is out of range: [{0}; {list.Count -1}]");
    }
  }

  (Func<ScriptValue> valueFn, ScriptValue.TypeEnum valueType) MakeCollectionCountGetter(MethodInfo propertyGetterInfo) {
    GetElementType(propertyGetterInfo);  // Ensure it's a collection.
    return (ValueFn, ScriptValue.TypeEnum.Number);

    ScriptValue ValueFn() {
      var list = ToList(propertyGetterInfo.Invoke(Component, []));
      return ScriptValue.FromInt(list.Count);
    };
  }

  Type GetElementType(MethodInfo propertyGetterInfo) {
    var propType = propertyGetterInfo.ReturnType;

    // Check if it's a regular C# collection.
    if (propType.IsGenericType && typeof(IList<>).IsAssignableFrom(propType.GetGenericTypeDefinition())) {
      return propType.GenericTypeArguments[0];
    }

    // Check if it's a TB type ReadOnlyHashSet. It doesn't follow contracts.
    var enumeratorMethod = propType.GetMethod("GetEnumerator");
    if (enumeratorMethod == null || !typeof(IEnumerator).IsAssignableFrom(enumeratorMethod.ReturnType)
        || !propType.IsGenericType || propType.GenericTypeArguments.Length != 1) {
      throw new ScriptError.ParsingError(
          $"Property {PropertyFullName} expected to be a collection, but was {propType}");
    }
    return propType.GenericTypeArguments[0];
  }

  static List<object> ToList(object value) {
    if (value is IEnumerable nativeEnumerable) {
      // Yay! A true C# collection.
      return nativeEnumerable.Cast<object>().ToList();
    }
    var getEnumeratorMethod = value.GetType().GetMethod("GetEnumerator");
    if (getEnumeratorMethod != null && getEnumeratorMethod.Invoke(value, []) is IEnumerator gameEnumerator) {
      // Nay. The Timberborn's ReadOnlyHashSet<>. Not a collection >:(
      var res = new List<object>();
      while (gameEnumerator.MoveNext()) {
        res.Add(gameEnumerator.Current);
      }
      return res;
    }
    throw new InvalidOperationException($"Value {value} is not a collection");
  }

  static BaseComponent GetComponentByName(BaseComponent baseComponent, string name) {
    if (name == "Inventory") {
      // Special case: the buildings can have more than one inventory. 
      return InventoryScriptableComponent.GetInventory(baseComponent, throwIfNotFound: false);
    }
    var components = baseComponent.AllComponents.OfType<BaseComponent>();
    return components.FirstOrDefault(x => x.GetType().Name == name);
  }
}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Compiles configured property/method access once; no reflection lookup occurs in setter callbacks.</summary>
static class HookMemberAccess {
  /// <summary>Result type and null-safe projection from an instance, or null for static access.</summary>
  public sealed record Reader(Type Type, Func<object, object> Read);

  /// <summary>Boundary types; enums and structures require provider-owned conversion.</summary>
  public static Type ValueType(string name) => name switch {
      "bool" => typeof(bool), "int" => typeof(int), "float" => typeof(float),
      "double" => typeof(double), "string" => typeof(string),
      _ => throw new ArgumentException("Value Type must be bool, int, float, double or string."),
  };

  /// <summary>Compiles a null-safe dot path of properties or parameterless methods written as Name().</summary>
  public static Reader CompileReader(Type type, bool isStatic, string path) {
    if (string.IsNullOrWhiteSpace(path)) {
      throw new ArgumentException("A non-empty Read path is required.");
    }
    var source = Expression.Parameter(typeof(object), "source");
    var result = Expression.Label(typeof(object));
    var variables = new List<ParameterExpression>();
    var body = new List<Expression>();
    Expression owner = isStatic ? null : Expression.Convert(source, type);
    foreach (var name in path.Split('.')) {
      var getter = name.EndsWith("()", StringComparison.Ordinal)
          ? AccessTools.Method(type, name.Substring(0, name.Length - 2), Type.EmptyTypes)
          : FindProperty(type, name).GetGetMethod(nonPublic: true);
      if (getter == null || getter.IsStatic != (owner == null) || getter.ReturnType == typeof(void)
          || getter.ReturnType.IsByRef || getter.ContainsGenericParameters) {
        throw new ArgumentException("Invalid property getter or parameterless method: " + type.FullName + "." + name);
      }
      if (owner != null && !owner.Type.IsValueType) {
        body.Add(Expression.IfThen(Expression.ReferenceEqual(owner, Expression.Constant(null, owner.Type)),
            Expression.Return(result, Expression.Constant(null, typeof(object)))));
      }
      var value = Expression.Variable(getter.ReturnType);
      variables.Add(value);
      body.Add(Expression.Assign(value, Expression.Call(owner, getter)));
      owner = value;
      type = getter.ReturnType;
    }
    body.Add(Expression.Label(result, Expression.Convert(owner, typeof(object))));
    var read = Expression.Lambda<Func<object, object>>(Expression.Block(variables, body), source).Compile();
    return new Reader(type, read);
  }

  /// <summary>Finds an exact overload and compiles an invocation with a fixed argument list of any size.</summary>
  public static Action<object, object[]> CompileAction(ConfiguredTarget target, string name, Type[] arguments) {
    if (string.IsNullOrWhiteSpace(name)) {
      throw new ArgumentException("Action Method is required.");
    }
    var method = AccessTools.Method(target.Type, name, arguments);
    if (method == null || method.IsStatic != target.IsStatic || method.ReturnType != typeof(void)
        || method.ContainsGenericParameters
        || !method.GetParameters().Select(x => x.ParameterType).SequenceEqual(arguments)) {
      throw new ArgumentException("Expected an exact void method overload: " + target.Type.FullName + "." + name);
    }
    var source = Expression.Parameter(typeof(object), "source");
    var values = Expression.Parameter(typeof(object[]), "arguments");
    var converted = arguments.Select((type, index) => Expression.Convert(
        Expression.ArrayIndex(values, Expression.Constant(index)), type));
    var call = Expression.Call(target.IsStatic ? null : Expression.Convert(source, target.Type), method, converted);
    return Expression.Lambda<Action<object, object[]>>(call, source, values).Compile();
  }

  /// <summary>Resolve an actual property setter; fields and indexers cannot be observed.</summary>
  public static MethodInfo FindSetter(ConfiguredTarget target, string name) {
    var setter = FindProperty(target.Type, name).GetSetMethod(nonPublic: true);
    if (setter == null || setter.IsStatic != target.IsStatic || setter.GetMethodBody() == null) {
      throw new ArgumentException("Expected a managed property setter: " + target.Type.FullName + "." + name);
    }
    return setter;
  }

  static PropertyInfo FindProperty(Type type, string name) {
    var property = AccessTools.Property(type, name)
        ?? type.GetInterfaces().Select(x => AccessTools.Property(x, name)).FirstOrDefault(x => x != null);
    if (property == null || property.GetIndexParameters().Length != 0) {
      throw new ArgumentException("Expected a property: " + type.FullName + "." + name);
    }
    return property;
  }
}

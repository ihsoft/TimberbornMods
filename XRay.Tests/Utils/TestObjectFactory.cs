using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace XRay.Tests;

static class TestObjectFactory {
  public static T Create<T>(params (string FieldName, object Value)[] fields) where T : class {
    var result = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    foreach (var (fieldName, value) in fields) {
      var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
      if (field == null) {
        throw new InvalidOperationException($"Field not found: {typeof(T).Name}.{fieldName}");
      }
      field.SetValue(result, value);
    }
    return result;
  }
}

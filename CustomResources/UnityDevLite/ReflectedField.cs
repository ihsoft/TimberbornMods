// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Reflection;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.Reflections {

/// <summary>Wrapper to implement efficient access to the class fields via reflection.</summary>
/// <remarks>It ignores access scope.</remarks>
/// <typeparam name="T">type of the class to get the field for.</typeparam>
/// <typeparam name="TV">type of the field value.</typeparam>
sealed class ReflectedField<T, TV> {
  readonly FieldInfo _fieldInfo;

  /// <summary>Creates the reflection for the field.</summary>
  /// <param name="fieldName">The name of the field.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the field cannot be obtained. Otherwise, the wrapper will only
  /// log the error and will ignoring all set/get operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  public ReflectedField(string fieldName, bool throwOnFailure = false) {
    _fieldInfo = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (_fieldInfo == null) {
      if (throwOnFailure) {
        throw new InvalidOperationException($"Cannot obtain field {typeof(T)}.{fieldName}");
      }
      DebugEx.Error("Cannot obtain field {0} from {1}", fieldName, typeof(T));
    } else {
      var fieldType = _fieldInfo.FieldType;
      if (typeof(TV) != fieldType) {
        if (throwOnFailure) {
          throw new InvalidOperationException($"Expected field type {typeof(TV)}, but found {fieldType}");
        }
        DebugEx.Error("Incompatible field types: requested={0}, actual={1}", typeof(TV), fieldType);
        _fieldInfo = null;
      }
    }
  }

  /// <summary>Indicates if the target field was found and ready to use.</summary>
  public bool IsValid() {
    return _fieldInfo != null;
  }

  /// <summary>Gets the field value or returns a default value if the field is not found.</summary>
  public TV Get(T instance) {
    return _fieldInfo != null ? (TV)_fieldInfo.GetValue(instance) : default(TV);
  }

  /// <summary>Gets the field value or returns the provided default value if the field is not found.</summary>
  public TV Get(T instance, TV defaultValue) {
    return _fieldInfo != null ? (TV)_fieldInfo.GetValue(instance) : defaultValue;
  }

  /// <summary>Sets the field value or does nothing if the field is not found.</summary>
  public void Set(T instance, TV value) {
    if (_fieldInfo != null) {
      _fieldInfo.SetValue(instance, value);
    }
  }
}


/// <summary>Wrapper to implement efficient access to the class fields via reflection.</summary>
/// <remarks>It ignores access scope.</remarks>
/// <typeparam name="TV">type of the field value.</typeparam>
sealed class ReflectedField<TV> {
  readonly FieldInfo _fieldInfo;

  /// <summary>Creates the reflection for the field.</summary>
  /// <param name="type">Type of the class to get the field for.</param>
  /// <param name="fieldName">The name of the field.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the field cannot be obtained. Otherwise, the wrapper will only
  /// log the error and will ignoring all set/get operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  public ReflectedField(IReflect type, string fieldName, bool throwOnFailure = false) {
    //MakeFromType(type, fieldName, throwOnFailure);
    _fieldInfo = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (_fieldInfo == null) {
      if (throwOnFailure) {
        throw new InvalidOperationException($"Cannot obtain field {type}.{fieldName}");
      }
      DebugEx.Error("Cannot obtain field {0} from {1}", fieldName, type);
    } else {
      var fieldType = _fieldInfo.FieldType;
      if (typeof(TV) != fieldType) {
        if (throwOnFailure) {
          throw new InvalidOperationException($"Expected field type {typeof(TV)}, but found {fieldType}");
        }
        DebugEx.Error("Incompatible field types: requested={0}, actual={1}", typeof(TV), fieldType);
        _fieldInfo = null;
      }
    }
  }

  /// <summary>Creates the reflection for the field.</summary>
  /// <param name="fullTypeName">Full type name. It can be internal type.</param>
  /// <param name="fieldName">The name of the field.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the field cannot be obtained. Otherwise, the wrapper will only
  /// log the error and will ignoring all set/get operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  public ReflectedField(string fullTypeName, string fieldName, bool throwOnFailure = false)
    : this(TypeUtils.GetInternalType(fullTypeName), fieldName, throwOnFailure) {
  }

  /// <summary>Indicates if the target field was found and ready to use.</summary>
  public bool IsValid() {
    return _fieldInfo != null;
  }

  /// <summary>Gets the field value or returns a default value if the field is not found.</summary>
  public TV Get(object instance) {
    return _fieldInfo != null ? (TV)_fieldInfo.GetValue(instance) : default;
  }

  /// <summary>Gets the field value or returns the provided default value if the field is not found.</summary>
  public TV Get(object instance, TV defaultValue) {
    return _fieldInfo != null ? (TV)_fieldInfo.GetValue(instance) : defaultValue;
  }

  /// <summary>Sets the field value or does nothing if the field is not found.</summary>
  public void Set(object instance, TV value) {
    if (_fieldInfo != null) {
      _fieldInfo.SetValue(instance, value);
    }
  }
}

}

// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using UnityDev.Utils.LogUtilsLite;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.Reflections;

/// <summary>Wrapper to implement efficient access to the class properties via reflection.</summary>
/// <remarks>It ignores access scope.</remarks>
/// <typeparam name="TV">type of the return value.</typeparam>
sealed class ReflectedProperty<TV> {
  readonly PropertyInfo _propertyInfo;

  /// <summary>Creates the reflection for the field.</summary>
  /// <param name="type">Type of the class to get the field for.</param>
  /// <param name="propertyName">The name of the field.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the field can't be obtained. Otherwise, the wrapper will only
  /// log the error and will ignore all set/get operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  public ReflectedProperty(IReflect type, string propertyName, bool throwOnFailure = false) {
    _propertyInfo =
        type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (_propertyInfo == null) {
      if (throwOnFailure) {
        throw new InvalidOperationException($"Cannot obtain field {type}.{propertyName}");
      }
      DebugEx.Error("Cannot obtain field {0} from {1}", propertyName, type);
    } else {
      var propertyType = _propertyInfo.PropertyType;
      if (typeof(TV).IsAssignableFrom(propertyType)) {
        return;
      }
      if (throwOnFailure) {
        throw new InvalidOperationException($"Expected field type {typeof(TV)}, but found {propertyType}");
      }
      DebugEx.Error("Incompatible field types: requested={0}, actual={1}", typeof(TV), propertyType);
      _propertyInfo = null;
    }
  }

  /// <summary>Creates the reflection for the field.</summary>
  /// <param name="fullTypeName">
  /// Full type name. It can be an internal type. It must exist or an exception will be thrown.
  /// </param>
  /// <param name="propertyName">The name of the field.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the field can't be obtained. Otherwise, the wrapper will only
  /// log the error and will ignore all set/get operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  /// <seealso cref="TypeUtils.GetInternalType"/>
  public ReflectedProperty(string fullTypeName, string propertyName, bool throwOnFailure = false)
      : this(TypeUtils.GetInternalType2(fullTypeName), propertyName, throwOnFailure) {
  }

  /// <summary>Indicates if the target field was found and ready to use.</summary>
  public bool IsValid() {
    return _propertyInfo != null;
  }

  /// <summary>Gets the field value or returns a default value if the field is not found.</summary>
  public TV Get(object instance) {
    return _propertyInfo != null ? (TV)_propertyInfo.GetValue(instance) : default;
  }

  /// <summary>Gets the field value or returns the provided default value if the field is not found.</summary>
  public TV Get(object instance, TV defaultValue) {
    return _propertyInfo != null ? (TV)_propertyInfo.GetValue(instance) : defaultValue;
  }

  /// <summary>Sets the field value or does nothing if the field is not found.</summary>
  public void Set(object instance, TV value) {
    if (_propertyInfo != null) {
      _propertyInfo.SetValue(instance, value);
    }
  }
}
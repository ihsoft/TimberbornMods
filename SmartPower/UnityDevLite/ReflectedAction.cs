// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Reflection;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.Reflections {

/// <summary>Wrapper to implement an efficient access to the class method via reflection.</summary>
/// <remarks>Implements access to a method that returns <c>void</c> and accepts no arguments.</remarks>
/// <typeparam name="T">type of the class to get the method for.</typeparam>
sealed class ReflectedAction<T> {
  readonly MethodInfo _methodInfo;

  /// <summary>Creates the reflection for the action.</summary>
  /// <param name="methodName">The name of the method.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the method cannot be obtained. Otherwise, the wrapper will only
  /// log the error and will be ignoring all invoke operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  public ReflectedAction(string methodName, bool throwOnFailure = false) {
    _methodInfo = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (_methodInfo != null) {
      return;
    }
    if (throwOnFailure) {
      throw new InvalidOperationException($"Cannot obtain method {typeof(T)}.{methodName}");
    }
    DebugEx.Error("Cannot obtain method {0} from {1}", methodName, typeof(T));
  }

  /// <summary>Indicates if the target method was found and ready to use.</summary>
  public bool IsValid() {
    return _methodInfo != null;
  }

  /// <summary>Invokes the method or NOOP if the method is not found.</summary>
  public void Invoke(T instance) {
    if (_methodInfo != null) {
      _methodInfo.Invoke(instance, new object[] {});
    }
  }
}

/// <summary>Wrapper to implement an efficient access to the class method via reflection.</summary>
/// <remarks>Implements access to a method that returns <c>void</c> and accepts exactly one argument.</remarks>
/// <typeparam name="T">type of the class to get the method for.</typeparam>
/// <typeparam name="TArg0">type of the action argument.</typeparam>
sealed class ReflectedAction<T, TArg0> {
  readonly MethodInfo _methodInfo;

  /// <summary>Creates the reflection for the action.</summary>
  /// <param name="methodName">The name of the method.</param>
  /// <param name="throwOnFailure">
  /// If <c>true</c> then the code will throw in case of the method cannot be obtained. Otherwise, the wrapper will only
  /// log the error and will be ignoring all invoke operations.
  /// </param>
  /// <seealso cref="IsValid"/>
  public ReflectedAction(string methodName,  bool throwOnFailure = false) {
    _methodInfo = typeof(T).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    if (_methodInfo != null) {
      return;
    }
    if (throwOnFailure) {
      throw new InvalidOperationException($"Cannot obtain method {typeof(T)}.{methodName}");
    }
    DebugEx.Error("Cannot obtain method {0} from {1}", methodName, typeof(T));
  }

  /// <summary>Indicates if the target method was found and ready to use.</summary>
  public bool IsValid() {
    return _methodInfo != null;
  }

  /// <summary>Invokes the method or NOOP if the method is not found.</summary>
  public void Invoke(T instance, TArg0 arg0) {
    if (_methodInfo != null) {
      _methodInfo.Invoke(instance, new object[] { arg0 });
    }
  }
}

}

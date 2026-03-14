// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

/// <summary>Helper class to deal with the reflections.</summary>
public static class ReflectionsHelper {
  /// <summary>Creates an instance of the type via reflection.</summary>
  /// <param name="typeId">Full type name.</param>
  /// <param name="throwOnError">
  /// If <c>true</c> then method throws when the type cannot be found. Otherwise, <c>null</c> is returned.
  /// </param>
  /// <typeparam name="TBaseType">the type to cast the instance to.</typeparam>
  public static TBaseType MakeInstance<TBaseType>(string typeId, bool throwOnError = true) where TBaseType : class {
    var type = GetType(typeId, typeof(TBaseType), throwOnError);
    return type != null ? (TBaseType)Activator.CreateInstance(type) : null;
  }

  /// <summary>Finds type definition by its full name.</summary>
  /// <param name="typeId">Full type name.</param>
  /// <param name="baseType">
  /// If provided, then <paramref name="typeId"/> type will be verified for being a descendant.
  /// </param>
  /// <param name="needDefaultConstructor">If <c>true</c> then type is required to have a default constructor.</param>
  /// <param name="throwOnError">
  /// If <c>true</c> then method throws when the type cannot be found. Otherwise, <c>null</c> is returned.
  /// </param>
  public static Type GetType(string typeId, Type baseType = null,
                             bool needDefaultConstructor = true, bool throwOnError = true) {
    if (string.IsNullOrEmpty(typeId)) {
      throw new ArgumentNullException(nameof(typeId));
    }
    var type = AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(typeId))
        .FirstOrDefault(t => t != null);
    string text = null;
    if (type == null) {
      text = $"Cannot find type for typeId: {typeId}";
    } else if (needDefaultConstructor && type.GetConstructor(Type.EmptyTypes) == null) {
      text = $"No default constructor in: {type}";
    } else if (baseType != null && !baseType.IsAssignableFrom(type)) {
      text = $"Incompatible types: {baseType} is not assignable from {type}";
    }
    if (text != null) {
      if (throwOnError) {
        throw new InvalidOperationException(text);
      }
      DebugEx.Error(text);
      return null;
    }
    return type;
  }
}

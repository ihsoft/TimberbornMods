// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.Reflections;

/// <summary>Various utils to deal with the types via reflections.</summary>
static class TypeUtils {
  /// <summary>Gets the type by its full name. Even if the type is internal.</summary>
  /// <exception cref="ArgumentException">if the type can't be found in the current appdomain.</exception>
  public static Type GetInternalType(string fullTypeName, bool throwIfNotFound = true) {
    var assemblyParts = fullTypeName.Split('.');
    var assemblyNameCandidates = new List<string>();
    for (var i = 1; i < assemblyParts.Length - 1; i++) {
      var assemblyName = string.Join(".", assemblyParts, 0, i);
      assemblyNameCandidates.Add(assemblyName);
    }
    var assembly = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(x => assemblyNameCandidates.Contains(x.GetName().Name));
    if (assembly == null) {
      if (throwIfNotFound) {
        throw new ArgumentException($"Cannot find assembly for type: {fullTypeName}");
      }
      return null;
    }
    var type = assembly.GetType(fullTypeName);
    if (type != null) {
      return type;
    }
    if (throwIfNotFound) {
      throw new ArgumentException($"Cannot get type 'fullTypeName' from assembly {assembly}");
    }
    DebugEx.Warning("Cannot get type '{0}' from assembly {1}", fullTypeName, assembly);
    return null;
  }
}

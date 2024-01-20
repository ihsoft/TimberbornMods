// UnityDev Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace UnityDev.Utils.Reflections {

/// <summary>Various utils to deal with the types via reflections.</summary>
static class TypeUtils {
  /// <summary>Gets the type by it's full name. Even if the type is internal.</summary>
  /// <exception cref="ArgumentException">if the type cannot be found in the current appdomain.</exception>
  public static Type GetInternalType(string fullTypeName) {
    var assemblyParts = fullTypeName.Split(new[] { '.' }, 3);
    if (assemblyParts.Length < 3) {
      throw new ArgumentException($"Invalid full type name: {fullTypeName}");
    }
    var assemblyName = $"{assemblyParts[0]}.{assemblyParts[1]}";
    var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName);
    if (assembly == null) {
      throw new ArgumentException($"Cannot find assembly: {assemblyName}");
    }
    var type = assembly.GetType(fullTypeName);
    if (type == null) {
      throw new ArgumentException($"Cannot get type 'fullTypeName' from assembly {assembly}");
    }
    return type;
  }
}

}

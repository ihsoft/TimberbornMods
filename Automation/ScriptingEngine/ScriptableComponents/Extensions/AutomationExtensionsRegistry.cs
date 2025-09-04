// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using Timberborn.Common;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Registry of automation extensions.</summary>
/// <remarks>
/// Mods that don't need hard dependency on Automation can find this type via reflections and get an instance via.
/// <seealso cref="IContainer.GetInstance"/>. Then, get the extensions they need via <see cref="GetExtension(string)"/>.
/// </remarks>
public class AutomationExtensionsRegistry {
  readonly Dictionary<string, IAutomationExtension> _extensions = [];

  /// <summary>Gets the extension by its type name.</summary>
  /// <param name="typeName">Type name of the extension to get.</param>
  /// <returns>The extension handler or <c>null</c> if nothing registered for the name.</returns>
  public IAutomationExtension GetExtension(string typeName) {
    return _extensions.GetOrDefault(typeName);
  }

  /// <summary>Gets the extension by its type.</summary>
  /// <typeparam name="T">Type of the extension to get.</typeparam>
  /// <returns>The extension handler or <c>null</c> if nothing registered for the type.</returns>
  public T GetExtension<T>() where T : class, IAutomationExtension {
    return _extensions.GetOrDefault(typeof(T).Name) as T;
  }

  /// <summary>Registers a new extension.</summary>
  internal void RegisterExtension(string name, IAutomationExtension extension) {
    if (_extensions.TryGetValue(name, out var value)) {
      DebugEx.Error("Extension already registered: {0} => {1}", name, value);
      return;
    }
    _extensions[name] = extension;
    DebugEx.Info("Registered automation extension: {0}", name);
  }
}

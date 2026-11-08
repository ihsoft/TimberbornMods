// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using System.Linq.Expressions;
using Bindito.Core;
using Timberborn.BaseComponentSystem;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

/// <summary>Resolves a configured source once per world, or a component separately for each building.</summary>
sealed class ConfiguredTarget {
  /// <summary>Normal absence of an optional integration's assembly, distinct from invalid metadata.</summary>
  public sealed class AssemblyNotLoadedException(string assemblyName)
      : Exception("Assembly is not loaded: " + assemblyName) {
    /// <summary>Simple assembly name from the configuration, used for the informational skip message.</summary>
    public string AssemblyName { get; } = assemblyName;
  }

  /// <summary>Exact runtime type resolved from the named assembly.</summary>
  public Type Type { get; }
  /// <summary>Whether the root members are static and need no source instance.</summary>
  public bool IsStatic { get; }
  /// <summary>Building component lookup, or null for global capabilities.</summary>
  public Func<BaseComponent, BaseComponent> ResolveBuilding { get; }

  /// <summary>Resolves metadata and compiles component lookup without constructing a provider service.</summary>
  public ConfiguredTarget(HookConfiguration.Source source) {
    if (string.IsNullOrWhiteSpace(source.Assembly) || string.IsNullOrWhiteSpace(source.Type)) {
      throw new ArgumentException("Source Assembly and Type are required.");
    }
    var assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(x => x.GetName().Name == source.Assembly)
        ?? throw new AssemblyNotLoadedException(source.Assembly);
    Type = assembly.GetType(source.Type, throwOnError: true);
    if (Type.IsValueType || Type.ContainsGenericParameters) {
      throw new ArgumentException("A source must be a closed reference type.");
    }
    switch (source.Scope) {
      case "Singleton":
        break;
      case "Static":
        IsStatic = true;
        break;
      case "Building":
        if (!typeof(BaseComponent).IsAssignableFrom(Type)) {
          throw new ArgumentException("A Building source must derive from BaseComponent.");
        }
        var building = Expression.Parameter(typeof(BaseComponent), "building");
        var call = Expression.Call(building, nameof(BaseComponent.GetComponent), [Type]);
        ResolveBuilding = Expression.Lambda<Func<BaseComponent, BaseComponent>>(
            Expression.Convert(call, typeof(BaseComponent)), building).Compile();
        break;
      default:
        throw new ArgumentException("Source Scope must be Singleton, Building or Static.");
    }
  }

  /// <summary>Resolves after singleton Load; installing hooks never constructs provider services.</summary>
  public void Bind(IContainer container) {
    if (!IsStatic && ResolveBuilding == null) {
      _singleton = container.GetInstance(Type);
    }
  }

  /// <summary>Returns the already-resolved source passed by the external scripting component.</summary>
  public object GetSource(BaseComponent building) => ResolveBuilding != null ? building : _singleton;

  /// <summary>Accept only this world's singleton, a live component of the target type, or a static call.</summary>
  public bool Matches(object instance) {
    // Constructors may call setters before Timberborn initializes the component's GameObject cache.
    return ResolveBuilding != null
        ? instance is BaseComponent component && Type.IsInstanceOfType(component)
            && component._componentCache != null && component
        : ReferenceEquals(instance, _singleton);
  }

  object _singleton;
}

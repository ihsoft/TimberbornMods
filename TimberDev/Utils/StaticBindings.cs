// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

/// <summary>Provider for the frequent singletons that can be obtained in a static context.</summary>
public sealed class StaticBindings {
  /// <summary>Factory for creation of the Bindito objects.</summary>
  public static IContainer DependencyContainer { get; private set; }

  StaticBindings(IContainer dependencyContainer) {
    DependencyContainer = dependencyContainer;
  }
}

using System;
using System.Collections.Generic;

namespace Bindito.Core;

[AttributeUsage(AttributeTargets.Method)]
public sealed class Inject : Attribute {
}

public interface IContainer {
  object GetInstance(Type type);
}

public sealed class TestContainer : IContainer {
  readonly Dictionary<Type, Func<object>> _factories = new();

  public void Register<T>(Func<T> factory) where T : class {
    _factories[typeof(T)] = factory;
  }

  public object GetInstance(Type type) {
    return _factories.TryGetValue(type, out var factory)
        ? factory()
        : Activator.CreateInstance(type);
  }
}

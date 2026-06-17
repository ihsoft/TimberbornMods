using System;
using System.Collections.Generic;
using System.Linq;

namespace Bindito.Core;

[AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method)]
public sealed class Inject : Attribute {
}

public sealed class BaseInstantiator {
}

public interface IContainer {
  object GetInstance(Type type);
  T GetInstance<T>();
}

public sealed class TestContainer : IContainer {
  readonly Dictionary<Type, Func<object>> _factories = new();

  public void Register<T>(Func<T> factory) where T : class {
    _factories[typeof(T)] = factory;
  }

  public object GetInstance(Type type) {
    var instance = _factories.TryGetValue(type, out var factory)
        ? factory()
        : Activator.CreateInstance(type);
    InjectDependencies(instance);
    return instance;
  }

  public T GetInstance<T>() {
    return (T)GetInstance(typeof(T));
  }

  void InjectDependencies(object instance) {
    var methods = instance.GetType()
        .GetMethods()
        .Where(x => x.GetCustomAttributes(typeof(Inject), inherit: true).Any());
    foreach (var method in methods) {
      var args = method.GetParameters()
          .Select(x => GetInstance(x.ParameterType))
          .ToArray();
      method.Invoke(instance, args);
    }
  }
}

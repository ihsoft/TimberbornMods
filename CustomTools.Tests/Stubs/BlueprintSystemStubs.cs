using System;
using System.Collections.Generic;

namespace Timberborn.BlueprintSystem;

[AttributeUsage(AttributeTargets.Property)]
public class SerializeAttribute : Attribute {
}

public abstract record ComponentSpec {
  public Blueprint Blueprint { get; } = new();

  public T GetSpec<T>() where T : class {
    return Blueprint.GetSpec<T>();
  }
}

public class Blueprint {
  readonly Dictionary<Type, object> _specs = new();

  public void AddSpec<T>(T spec) where T : class {
    _specs[typeof(T)] = spec;
  }

  public T GetSpec<T>() where T : class {
    return _specs.TryGetValue(typeof(T), out var spec) ? (T)spec : null;
  }
}

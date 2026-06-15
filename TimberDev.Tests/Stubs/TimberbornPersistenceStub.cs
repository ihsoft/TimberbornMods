using System;
using System.Collections.Generic;

namespace Timberborn.Persistence;

public readonly struct PropertyKey<T> {
  public readonly string Name;

  public PropertyKey(string name) {
    Name = name;
  }
}

public interface IValueSerializer<T> {
}

public interface IObjectLoader {
  bool Has<T>(PropertyKey<T> key);
  T Get<T>(PropertyKey<T> key);
  T Get<T>(PropertyKey<T> key, IValueSerializer<T> serializer);
}

public sealed class TestObjectLoader : IObjectLoader {
  readonly Dictionary<string, object> _values = new();

  public void Set<T>(PropertyKey<T> key, T value) {
    _values[key.Name] = value;
  }

  public bool Has<T>(PropertyKey<T> key) {
    return _values.ContainsKey(key.Name);
  }

  public T Get<T>(PropertyKey<T> key) {
    return (T)_values[key.Name];
  }

  public T Get<T>(PropertyKey<T> key, IValueSerializer<T> serializer) {
    return Get(key);
  }
}

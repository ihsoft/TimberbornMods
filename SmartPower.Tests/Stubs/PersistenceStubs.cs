using System.Collections.Generic;

namespace Timberborn.Persistence;

public sealed class ComponentKey {
  public string Name { get; }

  public ComponentKey(string name) {
    Name = name;
  }
}

public sealed class PropertyKey<T> {
  public string Name { get; }

  public PropertyKey(string name) {
    Name = name;
  }
}

public interface IEntitySaver {
  IObjectSaver GetComponent(ComponentKey key);
}

public interface IObjectSaver {
  void Set<T>(PropertyKey<T> key, T value);
}

public interface IEntityLoader {
  bool TryGetComponent(ComponentKey key, out IObjectLoader state);
}

public interface IObjectLoader {
  T GetValueOrDefault<T>(PropertyKey<T> key);
  T GetValueOrDefault<T>(PropertyKey<T> key, T defaultValue);
}

public sealed class ObjectState : IObjectSaver, IObjectLoader {
  readonly Dictionary<string, object> _values = new();

  public void Set<T>(PropertyKey<T> key, T value) {
    _values[key.Name] = value;
  }

  public T GetValueOrDefault<T>(PropertyKey<T> key) {
    return GetValueOrDefault(key, default(T));
  }

  public T GetValueOrDefault<T>(PropertyKey<T> key, T defaultValue) {
    return _values.TryGetValue(key.Name, out var value) ? (T)value : defaultValue;
  }
}

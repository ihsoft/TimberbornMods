using System.Collections.Generic;

namespace Timberborn.Persistence;

public readonly record struct ComponentKey(string Key) {
  public ComponentKey(System.Type type) : this(type.FullName) {
  }
}

public readonly record struct PropertyKey<T>(string Key);

public interface IObjectSaver {
  void Set<T>(PropertyKey<T> key, T value);
}

public interface IObjectLoader {
  T Get<T>(PropertyKey<T> key);
}

public interface IEntitySaver {
  IObjectSaver GetComponent(ComponentKey componentKey);
}

public interface IEntityLoader {
  bool TryGetComponent(ComponentKey componentKey, out IObjectLoader objectLoader);
}

public sealed class EntityState : IEntitySaver, IEntityLoader {
  public ObjectState Component { get; } = new();

  public IObjectSaver GetComponent(ComponentKey componentKey) {
    return Component;
  }

  public bool TryGetComponent(ComponentKey componentKey, out IObjectLoader objectLoader) {
    objectLoader = Component;
    return Component.HasValues;
  }
}

public sealed class ObjectState : IObjectSaver, IObjectLoader {
  readonly Dictionary<string, object> _values = [];

  public bool HasValues => _values.Count > 0;

  public void Set<T>(PropertyKey<T> key, T value) {
    _values[key.Key] = value;
  }

  public T Get<T>(PropertyKey<T> key) {
    return (T)_values[key.Key];
  }

  public T Get<T>(string key) {
    return (T)_values[key];
  }
}

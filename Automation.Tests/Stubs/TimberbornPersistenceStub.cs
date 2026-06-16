using System.Collections.Generic;

namespace Timberborn.Persistence {
  public sealed class ComponentKey {
    public string Name { get; }

    public ComponentKey(string name) {
      Name = name;
    }
  }

  public sealed class ListKey<T> {
    public string Name { get; }

    public ListKey(string name) {
      Name = name;
    }
  }

  public sealed class IObjectSaver {
    public readonly Dictionary<string, object> Values = new();

    public void Set<T>(ListKey<T> key, IList<T> value, object serializer = null) {
      Values[key.Name] = value;
    }
  }

  public sealed class IObjectLoader {
    readonly Dictionary<string, object> _values;

    public IObjectLoader(Dictionary<string, object> values) {
      _values = values;
    }

    public bool Has<T>(ListKey<T> key) {
      return _values.ContainsKey(key.Name);
    }

    public IList<T> Get<T>(ListKey<T> key, object serializer = null) {
      return (IList<T>)_values[key.Name];
    }
  }
  public sealed class SingletonKey {
    public string Name { get; }

    public SingletonKey(string name) {
      Name = name;
    }
  }
}

namespace Timberborn.WorldPersistence {
  using System.Collections.Generic;
  using Timberborn.Persistence;

  public interface IPersistentEntity {
    void Save(IEntitySaver entitySaver);
    void Load(IEntityLoader entityLoader);
  }

  public interface IEntitySaver {
    IObjectSaver GetComponent(ComponentKey componentKey);
  }

  public interface IEntityLoader {
    bool TryGetComponent(ComponentKey componentKey, out IObjectLoader objectLoader);
  }

  public interface ISaveableSingleton {
    void Save(ISingletonSaver singletonSaver);
  }

  public interface ISingletonSaver {
    IObjectSaver GetSingleton(SingletonKey singletonKey);
  }

  public interface ISingletonLoader {
    bool TryGetSingleton(SingletonKey singletonKey, out IObjectLoader objectLoader);
  }

  public sealed class TestEntitySaver : IEntitySaver {
    public readonly Dictionary<string, IObjectSaver> Components = new();

    public IObjectSaver GetComponent(ComponentKey componentKey) {
      var component = new IObjectSaver();
      Components[componentKey.Name] = component;
      return component;
    }
  }

  public sealed class TestEntityLoader : IEntityLoader {
    readonly Dictionary<string, IObjectLoader> _components = new();

    public void SetComponent(ComponentKey componentKey, IObjectLoader objectLoader) {
      _components[componentKey.Name] = objectLoader;
    }

    public bool TryGetComponent(ComponentKey componentKey, out IObjectLoader objectLoader) {
      return _components.TryGetValue(componentKey.Name, out objectLoader);
    }
  }
}

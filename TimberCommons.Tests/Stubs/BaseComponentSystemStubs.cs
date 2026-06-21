using System.Collections.Generic;

namespace Timberborn.BaseComponentSystem;

public class BaseComponent {
  readonly Dictionary<System.Type, object> _components = new();
  public List<object> AllComponents { get; } = [];

  public void SetComponent<T>(T component) where T : class {
    _components[typeof(T)] = component;
    if (!AllComponents.Contains(component)) {
      AllComponents.Add(component);
    }
  }

  public T GetComponent<T>() where T : class {
    return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
  }

  public static bool operator true(BaseComponent component) {
    return component != null;
  }

  public static bool operator false(BaseComponent component) {
    return component == null;
  }

  public static bool operator !(BaseComponent component) {
    return component == null;
  }

  public static implicit operator bool(BaseComponent component) {
    return component != null;
  }
}

public interface IAwakableComponent {
  void Awake();
}

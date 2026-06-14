using System.Collections.Generic;

namespace Timberborn.BaseComponentSystem;

public class BaseComponent {
  readonly Dictionary<System.Type, object> _components = new();

  public string Name { get; set; } = "";

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

  public T GetComponent<T>() where T : class {
    return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
  }

  public void SetComponent<T>(T component) where T : class {
    _components[typeof(T)] = component;
  }
}

public abstract class TickableComponent : BaseComponent {
  public bool Enabled { get; private set; } = true;

  public virtual void Tick() {
  }

  protected void EnableComponent() {
    Enabled = true;
  }

  protected void DisableComponent() {
    Enabled = false;
  }
}

public interface IAwakableComponent {
  void Awake();
}

using System.Collections.Generic;
using UnityEngine;

namespace Timberborn.BaseComponentSystem {
  public class BaseComponent {
    readonly Dictionary<System.Type, object> _components = new();

    public string Name { get; set; }
    public List<object> AllComponents { get; } = [];
    public MonoBehaviour _componentCache = new();

    public void SetComponent<T>(T component) where T : class {
      _components[typeof(T)] = component;
    }

    public T GetComponent<T>() where T : class {
      return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }

    public static bool operator !(BaseComponent component) {
      return component == null;
    }
  }

  public interface IAwakableComponent {
    void Awake();
  }
}

namespace Timberborn.BlockSystem {
  public sealed class BlockObject {
    public bool IsFinished { get; set; }
  }

  public interface IFinishedStateListener {
    void OnEnterFinishedState();
    void OnExitFinishedState();
  }
}

namespace Timberborn.DuplicationSystem {
  public interface IDuplicable {
    bool IsDuplicable { get; }
  }

  public interface IDuplicable<T> : IDuplicable {
    void DuplicateFrom(T source);
  }
}

namespace Timberborn.EntitySystem {
  public sealed class EntityComponent : Timberborn.BaseComponentSystem.BaseComponent {
    public object EntityId { get; set; } = "entity";
  }

  public interface IInitializableEntity {
    void InitializeEntity();
  }

  public interface IDeletableEntity {
    void DeleteEntity();
  }
}

namespace Timberborn.Localization {
  public interface ILoc {
    string T(string key, params object[] args);
  }
}

namespace Timberborn.SingletonSystem {
  public interface ILoadableSingleton {
    void Load();
  }

  public interface IPostLoadableSingleton {
  }

  [System.AttributeUsage(System.AttributeTargets.Method)]
  public sealed class OnEventAttribute : System.Attribute {
  }
}

namespace Timberborn.Common {
  public sealed class EventBus {
    public readonly List<object> RegisteredObjects = [];

    public void Register(object obj) {
      RegisteredObjects.Add(obj);
    }
  }
}

namespace Timberborn.StatusSystem {
  public sealed class StatusSubject {
    public readonly List<StatusToggle> RegisteredStatuses = [];

    public void RegisterStatus(StatusToggle statusToggle) {
      RegisteredStatuses.Add(statusToggle);
    }
  }

  public sealed class StatusToggle {
    public bool Active { get; private set; }

    public static StatusToggle CreatePriorityStatusWithAlertAndFloatingIcon(
        string icon, string description, string alert) {
      return new StatusToggle();
    }

    public void Activate() {
      Active = true;
    }

    public void Deactivate() {
      Active = false;
    }
  }
}

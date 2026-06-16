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

    public static implicit operator bool(BaseComponent component) {
      return component != null;
    }

    public static bool operator true(BaseComponent component) {
      return component != null;
    }

    public static bool operator false(BaseComponent component) {
      return component == null;
    }
  }

  public interface IAwakableComponent {
    void Awake();
  }
}

namespace Timberborn.AutomationBuildings {
  public sealed class Lever : Timberborn.BaseComponentSystem.BaseComponent {
    public bool IsOn { get; private set; }

    public void SwitchOn() {
      IsOn = true;
    }

    public void SwitchOff() {
      IsOn = false;
    }
  }
}

namespace Timberborn.Automation {
  public enum AutomatorState {
    Off,
    On,
  }

  public sealed class Automator : Timberborn.BaseComponentSystem.BaseComponent {
    public bool IsTransmitter { get; init; }
    public AutomatorState State { get; set; }
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

namespace Timberborn.Buildings {
  public sealed class PausableBuilding : Timberborn.BaseComponentSystem.BaseComponent {
    public bool Pausable { get; init; } = true;
    public bool Paused { get; private set; }

    public bool IsPausable() {
      return Pausable;
    }

    public void Pause() {
      Paused = true;
    }

    public void Resume() {
      Paused = false;
    }
  }
}

namespace Timberborn.ConstructionSites {
  public sealed class ConstructionSite : Timberborn.BaseComponentSystem.BaseComponent {
    public event System.EventHandler OnConstructionSiteProgressed;
    public float BuildTimeProgress { get; init; }

    public void Progress() {
      OnConstructionSiteProgressed?.Invoke(this, System.EventArgs.Empty);
    }
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

namespace Timberborn.Hauling {
  public sealed class HaulPrioritizable : Timberborn.BaseComponentSystem.BaseComponent {
    public bool Prioritized { get; set; }
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
  public static class DictionaryExtensions {
    public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key) {
      return dictionary.TryGetValue(key, out var value) ? value : default;
    }
  }

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

namespace Timberborn.WaterBuildings {
  public sealed class Floodgate : Timberborn.BaseComponentSystem.BaseComponent {
    public float Height { get; set; }
    public int MaxHeight { get; init; } = 1;
    public int SetHeightCalls { get; private set; }

    public void SetHeight(float height) {
      Height = height;
      SetHeightCalls++;
    }
  }
}

namespace Timberborn.WaterSourceSystem {
  public sealed class WaterSourceRegulator : Timberborn.BaseComponentSystem.BaseComponent {
    public bool IsOpen { get; private set; }

    public void Open() {
      IsOpen = true;
    }

    public void Close() {
      IsOpen = false;
    }
  }
}

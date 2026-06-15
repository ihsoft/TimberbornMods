using System.Collections.Generic;

namespace Timberborn.BaseComponentSystem {
  public class BaseComponent {
    readonly Dictionary<System.Type, object> _components = new();

    public void SetComponent<T>(T component) where T : class {
      _components[typeof(T)] = component;
    }

    public T GetComponent<T>() where T : class {
      return _components.TryGetValue(typeof(T), out var component) ? (T)component : null;
    }
  }
}

namespace Timberborn.ConstructionSites {
  public static class ConstructionSiteInventoryInitializer {
    public const string InventoryComponentName = "ConstructionSiteInventory";
  }
}

namespace Timberborn.InventorySystem {
  public sealed class Inventories {
    public readonly List<Inventory> AllInventories = new();

    public static bool operator !(Inventories inventories) {
      return inventories == null;
    }
  }

  public sealed class Inventory {
    public string ComponentName { get; set; }

    public static bool operator !(Inventory inventory) {
      return inventory == null;
    }
  }
}

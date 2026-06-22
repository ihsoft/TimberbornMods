using System;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using UnityEngine;

namespace Timberborn.EntitySystem {
  public interface IInitializableEntity {
    void InitializeEntity();
  }

  public interface IPostInitializableEntity {
    void PostInitializeEntity();
  }

  public sealed class EntityDeletedEvent {
    public BaseComponent Entity { get; }

    public EntityDeletedEvent(BaseComponent entity) {
      Entity = entity;
    }
  }
}

namespace Timberborn.MapIndexSystem {
  public sealed class MapIndexService {
    public Vector2Int TerrainSize { get; init; } = new(100, 100);
  }
}

namespace Timberborn.MechanicalSystem {
  public sealed class MechanicalNode : BaseComponent {
  }
}

namespace Timberborn.RangedEffectBuildingUI {
  public sealed class EnteredFinishedStateEvent {
    public BlockObject BlockObject { get; }

    public EnteredFinishedStateEvent(BlockObject blockObject) {
      BlockObject = blockObject;
    }
  }
}

namespace Timberborn.SelectionSystem {
  public interface ISelectionListener {
    void OnSelect();
    void OnUnselect();
  }

  public sealed class SelectableObject : BaseComponent {
  }

  public sealed class SelectableObjectSelectedEvent {
    public SelectableObject SelectableObject { get; }

    public SelectableObjectSelectedEvent(SelectableObject selectableObject) {
      SelectableObject = selectableObject;
    }
  }

  public sealed class SelectableObjectUnselectedEvent {
    public SelectableObject SelectableObject { get; }

    public SelectableObjectUnselectedEvent(SelectableObject selectableObject) {
      SelectableObject = selectableObject;
    }
  }
}

namespace Timberborn.SoilBarrierSystem {
  public sealed class SoilBarrierSpec : BaseComponent {
    public bool BlockFullMoisture { get; init; }
  }
}

namespace Timberborn.TerrainSystem {
  public sealed class TerrainMap {
    public event EventHandler<Vector3Int> TerrainAdded;
    public event EventHandler<Vector3Int> TerrainRemoved;

    public void AddTerrain(Vector3Int coordinates) {
      TerrainAdded?.Invoke(this, coordinates);
    }

    public void RemoveTerrain(Vector3Int coordinates) {
      TerrainRemoved?.Invoke(this, coordinates);
    }
  }

  public interface ITerrainService {
    bool OnGround(Vector3Int coordinates);
  }
}

namespace Timberborn.TickSystem {
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

  public interface ILateTickable {
  }
}

namespace UnityDev.Utils.LogUtilsLite {
  public static class HostedDebugLog {
    public static void Fine(object host, string message, params object[] args) {
    }

    public static void Error(object host, string message, params object[] args) {
    }
  }
}

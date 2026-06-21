using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.SelectionSystem;
using UnityEngine;

namespace Timberborn.BlockingSystem {
  public sealed class BlockableObject : BaseComponent {
    public bool IsUnblocked { get; set; } = true;
    public event EventHandler ObjectBlocked;
    public event EventHandler ObjectUnblocked;

    public void Block() {
      IsUnblocked = false;
      ObjectBlocked?.Invoke(this, EventArgs.Empty);
    }

    public void Unblock() {
      IsUnblocked = true;
      ObjectUnblocked?.Invoke(this, EventArgs.Empty);
    }
  }
}

namespace Timberborn.BuildingRange {
  public interface IBuildingWithRange {
    IEnumerable<Vector3Int> GetBlocksInRange();
    IEnumerable<BaseComponent> GetObjectsInRange();
    string RangeName { get; }
  }

  public sealed class BuildingWithRangeUpdateService {
    public void OnSelectableObjectUnselected(SelectableObjectUnselectedEvent e) {
    }

    public void OnSelectableObjectSelected(SelectableObjectSelectedEvent e) {
    }
  }
}

namespace Timberborn.Buildings {
  public interface IPostPlacementChangeListener {
    void OnPostPlacementChanged();
  }

  public interface IFinishedPausable {
  }

  public interface IBuildingEfficiencyProvider {
    float Efficiency { get; }
  }
}

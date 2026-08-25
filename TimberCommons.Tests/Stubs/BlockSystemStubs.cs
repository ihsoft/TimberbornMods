using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using UnityEngine;

namespace Timberborn.BlockSystem;

public sealed class BlockObject : BaseComponent {
  public Vector3Int Coordinates { get; init; }
  public bool IsFinished { get; init; }
  public PositionedBlocks PositionedBlocks { get; init; } = new();
  public Placement Placement { get; init; } = new();
}

public sealed class BlockService {
  readonly Dictionary<Vector3Int, BaseComponent> _bottomObjects = [];

  public void SetBottomObjectComponentAt<T>(Vector3Int coordinates, T component) where T : BaseComponent {
    _bottomObjects[coordinates] = component;
  }

  public T GetBottomObjectComponentAt<T>(Vector3Int coordinates) where T : class {
    if (!_bottomObjects.TryGetValue(coordinates, out var blockObject)) {
      return null;
    }
    return blockObject as T ?? blockObject.GetComponent<T>();
  }
}

public interface IFinishedStateListener {
  void OnEnterFinishedState();
  void OnExitFinishedState();
}

public sealed class PositionedBlocks {
  readonly List<PositionedBlock> _blocks = [];

  public IEnumerable<Vector3Int> GetFoundationCoordinates() {
    return _blocks.Select(block => block.Coordinates);
  }

  public IEnumerable<PositionedBlock> GetAllBlocks() {
    return _blocks;
  }

  public void AddBlock(Vector3Int coordinates, MatterBelow matterBelow = MatterBelow.Ground) {
    _blocks.Add(new PositionedBlock(coordinates, matterBelow));
  }
}

public sealed class PositionedBlock {
  public Vector3Int Coordinates { get; }
  public MatterBelow MatterBelow { get; }

  public PositionedBlock(Vector3Int coordinates, MatterBelow matterBelow) {
    Coordinates = coordinates;
    MatterBelow = matterBelow;
  }
}

public sealed class Placement {
  public Vector3Int Coordinates { get; init; }
}

public enum MatterBelow {
  Ground,
  GroundOrStackable,
  Other,
}

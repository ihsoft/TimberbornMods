using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using UnityEngine;

namespace Timberborn.BlockSystem;

public sealed class BlockObject : BaseComponent {
  public Vector3Int Coordinates { get; init; }
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

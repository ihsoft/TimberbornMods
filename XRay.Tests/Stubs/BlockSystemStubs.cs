using System.Collections.Generic;
using UnityEngine;

namespace Timberborn.BlockSystem;

class PlaceableBlockObjectSpec {
  public BlockObjectSpec BlockObjectSpec { get; set; }

  public bool CanBeAttachedToTerrainSide { get; set; }

  public Vector3Int CustomPivot { get; set; }

  public T GetSpec<T>() where T : class {
    return BlockObjectSpec as T;
  }
}

class BlockObjectSpec {
  public IReadOnlyList<BlockSpec> Blocks { get; set; }
}

class BlockSpec {
  public bool Underground { get; set; }
}

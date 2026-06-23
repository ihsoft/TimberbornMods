using System.Collections.Generic;
using UnityEngine;

namespace Timberborn.TerrainSystem;

interface ITerrainService {
  bool Contains(Vector2Int coordinates);

  bool Contains(Vector3Int coordinates);

  bool Underground(Vector3Int coordinates);
}

class FakeTerrainService : ITerrainService {
  readonly HashSet<Vector2Int> _columns = [];
  readonly HashSet<Vector3Int> _coordinates = [];
  readonly HashSet<Vector3Int> _underground = [];

  public void AddColumn(Vector2Int coordinates) {
    _columns.Add(coordinates);
  }

  public void AddTerrain(Vector3Int coordinates) {
    _coordinates.Add(coordinates);
    _columns.Add(new Vector2Int(coordinates.x, coordinates.y));
  }

  public void AddUnderground(Vector3Int coordinates) {
    AddTerrain(coordinates);
    _underground.Add(coordinates);
  }

  public bool Contains(Vector2Int coordinates) {
    return _columns.Contains(coordinates);
  }

  public bool Contains(Vector3Int coordinates) {
    return _coordinates.Contains(coordinates);
  }

  public bool Underground(Vector3Int coordinates) {
    return _underground.Contains(coordinates);
  }
}

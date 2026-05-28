// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using IgorZ.XRay.Settings;
using Timberborn.MapStateSystem;
using Timberborn.RootProviders;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IgorZ.XRay.Core;

class WireframeTerrainMeshService(TerrainMap terrainMap, MapSize mapSize, RootObjectProvider rootObjectProvider,
                                  RendererFactory rendererFactory, ColorSettings colorSettings)
    : ILoadableSingleton, ILateUpdatableSingleton {

  // Chunk size directly affects incremental rebuild cost.
  //
  // Approximate worst-case vertex counts for contour rendering (checkerboard-like terrain with maximum contour
  // complexity):
  //
  //  16x16 -> ~3k vertices
  //  32x32 -> ~12k vertices
  //  64x64 -> ~49k vertices
  //
  // Larger chunks reduce renderer/draw-call overhead, but dramatically increase rebuild cost and approach Unity's
  // 16-bit mesh vertex limit (~65k vertices).
  //
  // 32x32 provides a good balance for dynamic terrain updates.
  const int XMeshSize = 32;
  const int YMeshSize = 32;

  #region Implementation of ILoadableSingleton

  /// <inheritdoc/>
  public void Load() {
    _root = rootObjectProvider.CreateRootObject("XRayWireframeTerrainMesh");
    terrainMap.TerrainAdded += OnTerrainAdded;
    terrainMap.TerrainRemoved += OnTerrainRemoved;
  }

  #endregion

  #region ILateUpdatableSingleton implementation

  /// <inheritdoc/>
  public void LateUpdateSingleton() {
    if (!_needUpdate) {
      return;
    }
    _needUpdate = false;

    if (_fullRebuildRequested || !_overlay) {
      _fullRebuildRequested = false;
      _dirtyChunks.Clear();
      RefreshOverlay();
      return;
    }

    RebuildDirtyChunks();
  }

  #endregion

  #region API

  /// <summary>Tells if the wire frame mesh is active and should be rendered.</summary>
  public bool IsActive { get; private set; }

  /// <summary>Activate the wire frame mesh that will stay as long as it's active.</summary>
  /// <remarks>
  /// The activation can be expensive as it may (but not required) need to rebuild the entire terrain mesh. The
  /// following terrain updates will trigger mesh rebuilds. It will add latency.
  /// </remarks>
  /// <seealso cref="IsActive"/>
  public void Activate() {
    IsActive = true;
    _fullRebuildRequested = true;
    _needUpdate = true;
  }

  /// <summary>Deactivate the wire frame mesh that will stop rendering until it's activated again.</summary>
  /// <remarks>All the meshes will be removed from the scene. No extra cost on terrain update.</remarks>
  /// <seealso cref="IsActive"/>
  public void Deactivate() {
    IsActive = false;
    _needUpdate = true;
  }

  #endregion

  #region Implementation

  GameObject _root;
  GameObject _overlay;
  Material _material;
  bool _needUpdate;
  bool _fullRebuildRequested;

  readonly record struct EdgeKey(Vector3Int A, Vector3Int B) {
    public static EdgeKey Create(Vector3Int a, Vector3Int b) =>
        Compare(a, b) <= 0 ? new EdgeKey(a, b) : new EdgeKey(b, a);
  }

  struct EdgeInfo {
    public int Total;
    public int Top;
    public int Bottom;
    public int West;
    public int East;
    public int South;
    public int North;
  }

  static readonly Vector3Int FaceTop = new(0, 0, 1);
  static readonly Vector3Int FaceBottom = new(0, 0, -1);
  static readonly Vector3Int FaceWest = new(-1, 0, 0);
  static readonly Vector3Int FaceEast = new(1, 0, 0);
  static readonly Vector3Int FaceSouth = new(0, -1, 0);
  static readonly Vector3Int FaceNorth = new(0, 1, 0);

  static readonly Vector3Int[] Faces = [
      FaceTop,
      FaceBottom,
      FaceWest,
      FaceEast,
      FaceSouth,
      FaceNorth,
  ];

  readonly Dictionary<EdgeKey, EdgeInfo> _edgeCache = new();
  readonly Dictionary<Vector2Int, MeshFilter> _chunkMeshFilters = new();
  readonly HashSet<Vector2Int> _dirtyChunks = [];

  void RefreshOverlay() {
    if (_overlay) {
      Object.Destroy(_overlay);
      _overlay = null;
    }
    _chunkMeshFilters.Clear();
    _dirtyChunks.Clear();
    if (!IsActive) {
      return;
    }
    _overlay = CreateWireOverlay();
    _overlay.transform.SetParent(_root.transform, false);
  }

  GameObject CreateWireOverlay() {
    BuildMeshEdgeCache();
    _material = rendererFactory.CreateTransparencyMaterial("WireMaterial", colorSettings.WireframeEdgeColor.Color);

    var size = mapSize.TerrainSize;
    var overlayObj = new GameObject("XRayWireframeTerrainMesh");
    for (var startX = 0; startX < size.x; startX += XMeshSize) {
      for (var startY = 0; startY < size.y; startY += YMeshSize) {
        var chunk = new Vector2Int(startX / XMeshSize, startY / YMeshSize);
        var meshObj = new GameObject($"Mesh:{chunk.x}-{chunk.y}");
        meshObj.transform.SetParent(overlayObj.transform, false);

        var mf = meshObj.AddComponent<MeshFilter>();
        _chunkMeshFilters[chunk] = mf;
        mf.sharedMesh = BuildChunkMesh(chunk);

        var mr = meshObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
      }
    }

    return overlayObj;
  }

  void BuildMeshEdgeCache() {
    _edgeCache.Clear();
    var size = mapSize.TerrainSize;
    for (var x = 0; x < size.x; x++) {
      for (var y = 0; y < size.y; y++) {
        for (var z = 0; z < size.z; z++) {
          var coordinates = new Vector3Int(x, y, z);
          if (IsSolid(coordinates)) {
            ChangeVisibleCubeFaces(coordinates, +1);
          }
        }
      }
    }
  }

  void RebuildDirtyChunks() {
    foreach (var chunk in _dirtyChunks) {
      if (!_chunkMeshFilters.TryGetValue(chunk, out var mf)) {
        continue;
      }
      if (mf.sharedMesh) {
        Object.Destroy(mf.sharedMesh);
      }
      mf.sharedMesh = BuildChunkMesh(chunk);
    }
    _dirtyChunks.Clear();
  }

  Mesh BuildChunkMesh(Vector2Int chunk) {
    var startX = chunk.x * XMeshSize;
    var startY = chunk.y * YMeshSize;
    var endX = Mathf.Min(startX + XMeshSize, mapSize.TerrainSize.x);
    var endY = Mathf.Min(startY + YMeshSize, mapSize.TerrainSize.y);

    var vertices = new List<Vector3>();
    var indices = new List<int>();
    foreach (var (edge, info) in _edgeCache) {
      if (!IsContourEdge(info) || !BelongsToChunk(edge, startX, endX, startY, endY)) {
        continue;
      }
      var i = vertices.Count;
      vertices.Add(edge.A);
      vertices.Add(edge.B);
      indices.Add(i);
      indices.Add(i + 1);
    }

    var mesh = new Mesh { name = $"XRayTileWireMesh:{chunk.x}-{chunk.y}" };
    mesh.SetVertices(vertices);
    mesh.SetIndices(indices, MeshTopology.Lines, 0);
    mesh.RecalculateBounds();
    return mesh;
  }

  void ApplyVoxelChange(Vector3Int coordinates, bool wasSolid, bool isSolid) {
    if (wasSolid == isSolid || !IsActive) {
      return;
    }
    if (!_overlay) {
      _fullRebuildRequested = true;
      _needUpdate = true;
      return;
    }
    if (isSolid) {
      AddSolidVoxel(coordinates);
    } else {
      RemoveSolidVoxel(coordinates);
    }
    _needUpdate = true;
  }

  void AddSolidVoxel(Vector3Int coordinates) {
    foreach (var normal in Faces) {
      var neighbor = coordinates + normal;
      if (IsSolid(neighbor)) {
        ChangeSingleFace(neighbor, -normal, -1);
      }
    }
    ChangeVisibleCubeFaces(coordinates, +1);
  }

  void RemoveSolidVoxel(Vector3Int coordinates) {
    ChangeVisibleCubeFaces(coordinates, -1);
    foreach (var normal in Faces) {
      var neighbor = coordinates + normal;
      if (IsSolid(neighbor)) {
        ChangeSingleFace(neighbor, -normal, +1);
      }
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  bool IsSolid(Vector3Int coordinates) {
    return terrainMap.IsTerrainVoxel(coordinates);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void ChangeVisibleCubeFaces(Vector3Int coordinates, int delta) {
    foreach (var normal in Faces) {
      if (!IsSolid(coordinates + normal)) {
        ChangeSingleFace(coordinates, normal, delta);
      }
    }
  }

  void ChangeSingleFace(Vector3Int coordinates, Vector3Int normal, int delta) {
    var x = coordinates.x;
    var y = coordinates.y;
    var z = coordinates.z;

    var p000 = new Vector3Int(x, z, y);
    var p100 = new Vector3Int(x + 1, z, y);
    var p010 = new Vector3Int(x, z + 1, y);
    var p110 = new Vector3Int(x + 1, z + 1, y);
    var p001 = new Vector3Int(x, z, y + 1);
    var p101 = new Vector3Int(x + 1, z, y + 1);
    var p011 = new Vector3Int(x, z + 1, y + 1);
    var p111 = new Vector3Int(x + 1, z + 1, y + 1);

    if (normal == FaceTop) {
      ChangeFaceEdges(p010, p110, p111, p011, normal, delta);
    } else if (normal == FaceBottom) {
      ChangeFaceEdges(p000, p001, p101, p100, normal, delta);
    } else if (normal == FaceWest) {
      ChangeFaceEdges(p000, p010, p011, p001, normal, delta);
    } else if (normal == FaceEast) {
      ChangeFaceEdges(p100, p101, p111, p110, normal, delta);
    } else if (normal == FaceSouth) {
      ChangeFaceEdges(p000, p100, p110, p010, normal, delta);
    } else if (normal == FaceNorth) {
      ChangeFaceEdges(p001, p011, p111, p101, normal, delta);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void ChangeFaceEdges(Vector3Int a, Vector3Int b, Vector3Int c, Vector3Int d, Vector3Int normal, int delta) {
    ChangeEdge(a, b, normal, delta);
    ChangeEdge(b, c, normal, delta);
    ChangeEdge(c, d, normal, delta);
    ChangeEdge(d, a, normal, delta);
  }

  void ChangeEdge(Vector3Int a, Vector3Int b, Vector3Int normal, int delta) {
    var key = EdgeKey.Create(a, b);
    _edgeCache.TryGetValue(key, out var info);
    var wasContour = IsContourEdge(info);
    AddNormal(ref info, normal, delta);
    if (info.Total <= 0) {
      _edgeCache.Remove(key);
    } else {
      _edgeCache[key] = info;
    }
    var isContour = info.Total > 0 && IsContourEdge(info);
    if (wasContour != isContour) {
      MarkChunkDirty(key);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void AddNormal(ref EdgeInfo info, Vector3Int normal, int delta) {
    info.Total += delta;
    if (normal == FaceTop) {
      info.Top += delta;
    } else if (normal == FaceBottom) {
      info.Bottom += delta;
    } else if (normal == FaceWest) {
      info.West += delta;
    } else if (normal == FaceEast) {
      info.East += delta;
    } else if (normal == FaceSouth) {
      info.South += delta;
    } else if (normal == FaceNorth) {
      info.North += delta;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void MarkChunkDirty(EdgeKey edge) {
    _needUpdate = true;
    _dirtyChunks.Add(GetOwnerChunk(edge));
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  Vector2Int GetOwnerChunk(EdgeKey edge) {
    var ownerX = Mathf.Min(edge.A.x, edge.B.x);
    var ownerY = Mathf.Min(edge.A.z, edge.B.z);
    var size = mapSize.TerrainSize;
    ownerX = Mathf.Clamp(ownerX, 0, size.x - 1);
    ownerY = Mathf.Clamp(ownerY, 0, size.y - 1);
    return new Vector2Int(ownerX / XMeshSize, ownerY / YMeshSize);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  bool BelongsToChunk(EdgeKey edge, int startX, int endX, int startY, int endY) {
    var ownerX = Mathf.Min(edge.A.x, edge.B.x);
    var ownerY = Mathf.Min(edge.A.z, edge.B.z);
    var size = mapSize.TerrainSize;
    ownerX = Mathf.Clamp(ownerX, 0, size.x - 1);
    ownerY = Mathf.Clamp(ownerY, 0, size.y - 1);
    return ownerX >= startX && ownerX < endX && ownerY >= startY && ownerY < endY;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool IsContourEdge(EdgeInfo info) {
    if (info.Total == 1) {
      return true;
    }
    var count = 0;
    if (info.Top > 0) {
      count++;
    }
    if (info.Bottom > 0) {
      count++;
    }
    if (info.West > 0) {
      count++;
    }
    if (info.East > 0) {
      count++;
    }
    if (info.South > 0) {
      count++;
    }
    if (info.North > 0) {
      count++;
    }
    return count > 1;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static int Compare(Vector3Int a, Vector3Int b) {
    var r = a.x.CompareTo(b.x);
    if (r != 0) {
      return r;
    }
    r = a.y.CompareTo(b.y);
    return r != 0 ? r : a.z.CompareTo(b.z);
  }

  #endregion

  #region Terrain changed event listeners

  void OnTerrainAdded(object sender, Vector3Int coordinates) {
    ApplyVoxelChange(coordinates, wasSolid: false, isSolid: true);
  }

  void OnTerrainRemoved(object sender, Vector3Int coordinates) {
    ApplyVoxelChange(coordinates, wasSolid: true, isSolid: false);
  }

  #endregion
}

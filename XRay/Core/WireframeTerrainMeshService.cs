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
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IgorZ.XRay.Core;

class WireframeTerrainMeshService(
    TerrainMap terrainMap,
    MapSize mapSize,
    ITerrainService terrainService,
    RootObjectProvider rootObjectProvider,
    RendererFactory rendererFactory,
    ColorSettings colorSettings)
    : ILoadableSingleton, ILateUpdatableSingleton {

  // const int XMeshSize = 48;
  // const int YMeshSize = 48;
  const int XMeshSize = 10;
  const int YMeshSize = 10;

  public void Activate() {
    _needOverlayMesh = true;
    _fullRebuildRequested = true;
    _needUpdate = true;
  }

  public void Deactivate() {
    _needOverlayMesh = false;
    _needUpdate = true;
  }

  public void Load() {
    _root = rootObjectProvider.CreateRootObject("XRayWireframeTerrainMesh");
    terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
    terrainMap.TerrainAdded += OnTerrainAdded;
    terrainMap.TerrainRemoved += OnTerrainRemoved;
  }

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

  /// <summary>
  /// Incremental update entry point. Call this after the terrain state has changed.
  /// </summary>
  public void ApplyVoxelChange(Vector3Int coordinates, bool wasSolid, bool isSolid) {
    if (wasSolid == isSolid) {
      return;
    }
    if (!_needOverlayMesh) {
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

  GameObject _root;
  GameObject _overlay;
  Material _material;
  bool _needUpdate;
  bool _needOverlayMesh;
  bool _fullRebuildRequested;

  readonly Dictionary<EdgeKey, EdgeInfo> _edgeCache = new();
  readonly Dictionary<Vector2Int, MeshFilter> _chunkMeshFilters = new();
  readonly HashSet<Vector2Int> _dirtyChunks = new();

  readonly record struct EdgeKey(Vector3Int A, Vector3Int B) {
    public static EdgeKey Create(Vector3Int a, Vector3Int b) =>
        Compare(a, b) <= 0 ? new EdgeKey(a, b) : new EdgeKey(b, a);
  }

  struct EdgeInfo {
    public int Total;
    public int Top;
    public int West;
    public int East;
    public int South;
    public int North;
  }

  static readonly Vector3Int FaceTop = new(0, 0, 1);
  static readonly Vector3Int FaceWest = new(-1, 0, 0);
  static readonly Vector3Int FaceEast = new(1, 0, 0);
  static readonly Vector3Int FaceSouth = new(0, -1, 0);
  static readonly Vector3Int FaceNorth = new(0, 1, 0);

  static readonly Vector3Int[] Faces = {
      FaceTop,
      FaceWest,
      FaceEast,
      FaceSouth,
      FaceNorth,
  };

  void RefreshOverlay() {
    if (_overlay) {
      Object.Destroy(_overlay);
      _overlay = null;
    }
    _chunkMeshFilters.Clear();
    _dirtyChunks.Clear();

    if (!_needOverlayMesh) {
      return;
    }

    _overlay = CreateWireOverlay();
    _overlay.transform.SetParent(_root.transform, false);
  }

  GameObject CreateWireOverlay() {
    var overlayObj = new GameObject("XRayWireframeTerrainMesh");

    BuildMeshEdgeCache();

    _material = rendererFactory.CreateTransparencyMaterial(
        "WireMaterial",
        colorSettings.WireframeEdgeColor.Color);

    var size = mapSize.TerrainSize;

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
          if (!IsSolid(coordinates)) {
            continue;
          }
          ChangeVisibleCubeFaces(coordinates, +1);
        }
      }
    }
  }

  void RebuildDirtyChunks() {
    foreach (var chunk in _dirtyChunks) {
      //FIXME: level
      DebugEx.Warning("*** Rebuilding chunk: {0}", chunk);
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
    //FIXME
    DebugEx.Warning("*** Marking chunk dirty: {0}", GetOwnerChunk(edge));
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
    return info.Total == 1 || CountUsedNormals(info) > 1;
  }

  static int CountUsedNormals(EdgeInfo info) {
    var count = 0;
    if (info.Top > 0) {
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
    return count;
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

  void OnTerrainAdded(object sender, Vector3Int coordinates) {
    //FIXME
    DebugEx.Warning("*** terrain added: {0}", coordinates);
    ApplyVoxelChange(coordinates, wasSolid: false, isSolid: true);
  }

  void OnTerrainRemoved(object sender, Vector3Int coordinates) {
    //FIXME
    DebugEx.Warning("*** terrain removed: {0}", coordinates);
    ApplyVoxelChange(coordinates, wasSolid: true, isSolid: false);
  }

  void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs terrainHeightChangeEventArgs) {
    // _fullRebuildRequested = true;
    // _needUpdate = true;
    DebugEx.Warning("*** terrain changed!");
  }
}

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

class WireframeTerrainMeshService(
    TerrainMap terrainMap, MapSize mapSize, ITerrainService terrainService, RootObjectProvider rootObjectProvider,
    RendererFactory rendererFactory, ColorSettings colorSettings)
    : ILoadableSingleton, ILateUpdatableSingleton {

  // FIXME: Older cards may need smaller mesh size
  const int XMeshSize = 48;
  const int YMeshSize = 48;

  #region API

  public void Activate() {
    _needOverlayMesh = true;
    _terrainDirty = true;
  }

  public void Deactivate() {
    _needOverlayMesh = false;
    _terrainDirty = true;
  }

  #endregion

  #region ILoadableSingleton implementation

  /// <inheritdoc/>
  public void Load() {
    _root = rootObjectProvider.CreateRootObject("XRayWireframeTerrainMesh");
    terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
  }

  #endregion

  #region ILateUpdatableSingleton implementation

  /// <summary>Tracks the dirty state of the terrain and refreshes the wireframe overlay if necessary.</summary>
  public void LateUpdateSingleton() {
    if (!_terrainDirty) {
      return;
    }
    _terrainDirty = false;
    RefreshOverlay();
  }

  #endregion

  #region Implementation

  GameObject _root;
  GameObject _overlay;
  bool _terrainDirty;
  bool _needOverlayMesh;

  readonly record struct EdgeKey(Vector3Int A, Vector3Int B) {
    public static EdgeKey Create(Vector3Int a, Vector3Int b) =>
        Compare(a, b) <= 0 ? new EdgeKey(a, b) : new EdgeKey(b, a);
  }

  struct EdgeInfo {
    public int FaceCount;
    public Vector3Int FirstNormal;
    public bool HasDifferentNormals;
  }

  readonly Dictionary<EdgeKey, EdgeInfo> _edgeCache = new();

  void RefreshOverlay() {
    if (_overlay) {
      Object.Destroy(_overlay);
      _overlay = null;
    }
    if (!_needOverlayMesh) {
      return;
    }
    _overlay = CreateWireOverlay();
    _overlay.transform.SetParent(_root.transform, false);
  }

  GameObject CreateWireOverlay() {
    var overlayObj = new GameObject("XRayWireframeTerrainMesh");
    BuildMeshEdgeCache();

    var mat = rendererFactory.CreateTransparencyMaterial("WireMaterial", colorSettings.WireframeEdgeColor.Color);
    var size = mapSize.TerrainSize;
    for (var startX = 0; startX < size.x; startX += XMeshSize) {
      for (var startY = 0; startY < size.y; startY += YMeshSize) {
        var meshX = startX / XMeshSize;
        var meshY = startY / YMeshSize;
        var meshObj = new GameObject($"Mesh:{meshX}-{meshY}");
        meshObj.transform.SetParent(overlayObj.transform, false);
        var mf = meshObj.AddComponent<MeshFilter>();
        var endX = Mathf.Min(startX + XMeshSize, size.x);
        var endY = Mathf.Min(startY + YMeshSize, size.y);
        mf.sharedMesh = BuildTileWireMesh(startX, endX, startY, endY, size.z);
        var mr = meshObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
      }
    }

    return overlayObj;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  bool IsSolid(Vector3Int coordinates) {
    return terrainMap.IsTerrainVoxel(coordinates);
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
          AddVisibleCubeEdges(coordinates);
        }
      }
    }
  }

  Mesh BuildTileWireMesh(int startX, int endX, int startY, int endY, int maxZ) {
    var vertices = new List<Vector3>();
    var indices = new List<int>();
    foreach (var (edge, info) in _edgeCache) {
      if (!IsContourEdge(info)) {
        continue;
      }
      if (!BelongsToChunk(edge, startX, endX, startY, endY)) {
        continue;
      }
      var i = vertices.Count;
      vertices.Add(edge.A);
      vertices.Add(edge.B);
      indices.Add(i);
      indices.Add(i + 1);
    }

    var mesh = new Mesh { name = "XRayTileWireMesh" };
    mesh.SetVertices(vertices);
    mesh.SetIndices(indices, MeshTopology.Lines, 0);
    mesh.RecalculateBounds();
    return mesh;
  }

  // top:    new Vector3Int(0, 0, 1)
  // bottom: new Vector3Int(0, 0, -1)
  // west:   new Vector3Int(-1, 0, 0)
  // east:   new Vector3Int(1, 0, 0)
  // south:  new Vector3Int(0, -1, 0)
  // north:  new Vector3Int(0, 1, 0)
  static readonly Vector3Int FaceTop = new(0, 0, 1);
  static readonly Vector3Int FaceWest = new(-1, 0, 0);
  static readonly Vector3Int FaceEast = new(1, 0, 0);
  static readonly Vector3Int FaceSouth = new(0, -1, 0);
  static readonly Vector3Int FaceNorth = new(0, 1, 0);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void AddVisibleCubeEdges(Vector3Int coordinates) {
    var x = coordinates.x;
    var y = coordinates.y;
    var z = coordinates.z;

    var p000 = new Vector3Int(x,     z,     y);
    var p100 = new Vector3Int(x + 1, z,     y);
    var p010 = new Vector3Int(x,     z + 1, y);
    var p110 = new Vector3Int(x + 1, z + 1, y);
    var p001 = new Vector3Int(x,     z,     y + 1);
    var p101 = new Vector3Int(x + 1, z,     y + 1);
    var p011 = new Vector3Int(x,     z + 1, y + 1);
    var p111 = new Vector3Int(x + 1, z + 1, y + 1);

    if (!IsSolid(coordinates + FaceTop)) {  // top
      RegisterFaceEdges(p010, p110, p111, p011, FaceTop);
    }
    if (!IsSolid(coordinates + FaceWest)) {  // west
      RegisterFaceEdges(p000, p010, p011, p001, FaceWest);
    }
    if (!IsSolid(coordinates + FaceEast)) {  // east
      RegisterFaceEdges(p100, p101, p111, p110, FaceEast);
    }
    if (!IsSolid(coordinates + FaceSouth)) {  // south
      RegisterFaceEdges(p000, p100, p110, p010, FaceSouth);
    }
    if (!IsSolid(coordinates + FaceNorth)) {  // north
      RegisterFaceEdges(p001, p011, p111, p101, FaceNorth);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void RegisterFaceEdges(Vector3Int a, Vector3Int b, Vector3Int c, Vector3Int d, Vector3Int normal) {
    RegisterEdge(a, b, normal);
    RegisterEdge(b, c, normal);
    RegisterEdge(c, d, normal);
    RegisterEdge(d, a, normal);
  }

  void RegisterEdge(Vector3Int a, Vector3Int b, Vector3Int normal) {
    var key = EdgeKey.Create(a, b);
    if (!_edgeCache.TryGetValue(key, out var info)) {
      _edgeCache.Add(key, new EdgeInfo {
          FaceCount = 1,
          FirstNormal = normal,
          HasDifferentNormals = false,
      });
      return;
    }

    info.FaceCount++;
    info.HasDifferentNormals |= info.FirstNormal != normal;
    //FIXME: record instead of struct?
    _edgeCache[key] = info;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static bool IsContourEdge(EdgeInfo info) {
    return info.FaceCount == 1 || info.HasDifferentNormals || info.FaceCount > 2;
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

  #region TerrainChange listener

  void OnTerrainHeightChanged(object sender, TerrainHeightChangeEventArgs terrainHeightChangeEventArgs) {
    //FIXME: check if active and need refresh?
    _terrainDirty = true;
  }

  #endregion
}

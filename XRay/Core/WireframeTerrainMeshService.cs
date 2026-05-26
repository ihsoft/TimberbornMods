// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Timberborn.MapStateSystem;
using Timberborn.RootProviders;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.XRay.Core;

class WireframeTerrainMeshService(
    TerrainMap terrainMap, MapSize mapSize, ITerrainService terrainService, RootObjectProvider rootObjectProvider,
    RendererFactory rendererFactory)
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
    //FIXME: to settings?
    var color = new Color(0.55f, 0.9f, 1f, 0.10f);
    var mat = rendererFactory.CreateTransparencyMaterial("WireMaterial", color);

    var size = mapSize.TerrainSize;
    _allEdges.Clear();
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

  readonly HashSet<(Vector3, Vector3)> _allEdges = [];

  bool IsSolid(Vector3Int coordinates) {
    return terrainMap.IsTerrainVoxel(coordinates);
  }

  Mesh BuildTileWireMesh(int startX, int endX, int startY, int endY, int maxZ) {
    var vertices = new List<Vector3>();
    var indices = new List<int>();
    var edges = new HashSet<(Vector3, Vector3)>();

    for (var x = startX; x < endX; x++) {
      for (var y = startY; y < endY; y++) {
        for (var z = 0; z < maxZ; z++) {
          var coordinates = new Vector3Int(x, y, z);
          if (!IsSolid(coordinates)) {
            continue;
          }
          AddVisibleCubeEdges(coordinates, edges);
        }
      }
    }

    foreach (var (a, b) in edges) {
      var i = vertices.Count;
      vertices.Add(a);
      vertices.Add(b);
      indices.Add(i);
      indices.Add(i + 1);
    }

    var mesh = new Mesh { name = "XRayTileWireMesh" };
    mesh.SetVertices(vertices);
    mesh.SetIndices(indices, MeshTopology.Lines, 0);
    mesh.RecalculateBounds();
    return mesh;
  }

  void AddVisibleCubeEdges(Vector3Int coordinates, HashSet<(Vector3, Vector3)> edges) {
    var x = coordinates.x;
    var y = coordinates.y;
    var z = coordinates.z;

    var p000 = new Vector3(x,     z,     y);
    var p100 = new Vector3(x + 1, z,     y);
    var p010 = new Vector3(x,     z + 1, y);
    var p110 = new Vector3(x + 1, z + 1, y);
    var p001 = new Vector3(x,     z,     y + 1);
    var p101 = new Vector3(x + 1, z,     y + 1);
    var p011 = new Vector3(x,     z + 1, y + 1);
    var p111 = new Vector3(x + 1, z + 1, y + 1);

    if (!IsSolid(new Vector3Int(x, y, z + 1))) {  // top
      AddFaceEdges(edges, p010, p110, p111, p011);
    }
    if (!IsSolid(new Vector3Int(x, y, z - 1))) {  // bottom, probably optional
      AddFaceEdges(edges, p000, p001, p101, p100);
    }
    if (!IsSolid(new Vector3Int(x - 1, y, z))) {  // west
      AddFaceEdges(edges, p000, p010, p011, p001);
    }
    if (!IsSolid(new Vector3Int(x + 1, y, z))) {  // east
      AddFaceEdges(edges, p100, p101, p111, p110);
    }
    if (!IsSolid(new Vector3Int(x, y - 1, z))) {  // south
      AddFaceEdges(edges, p000, p100, p110, p010);
    }
    if (!IsSolid(new Vector3Int(x, y + 1, z))) {  // north
      AddFaceEdges(edges, p001, p011, p111, p101);
    }
  }

  void AddFaceEdges(HashSet<(Vector3, Vector3)> edges, Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
    AddEdge(edges, a, b);
    AddEdge(edges, b, c);
    AddEdge(edges, c, d);
    AddEdge(edges, d, a);
  }

  void AddEdge(HashSet<(Vector3, Vector3)> edges, Vector3 a, Vector3 b) {
    if (Compare(a, b) > 0) {
      (a, b) = (b, a);
    }
    if (_allEdges.Add((a, b))) {
      edges.Add((a, b));
    }
  }

  static int Compare(Vector3 a, Vector3 b) {
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

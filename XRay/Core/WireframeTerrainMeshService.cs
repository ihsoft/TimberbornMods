// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    TerrainMap terrainMap, MapSize mapSize, RootObjectProvider rootObjectProvider, RendererFactory rendererFactory,
    ColorSettings colorSettings)
    : IPostLoadableSingleton, ILateUpdatableSingleton {

  // This is how much time is allowed for building the chunk meshes before it is considered too slow. If there are more 
  // chunks left, then they will be updated in the next frame. That being said, the full scene update may not be
  // complete in one frame. It will become clear to the user that the meshes are being built, but it will unblock the
  // game interface, which is most important for the user experience.
  const int MaxChunkMeshBuildTimeMs = 50;

  // Same as MaxChunkMeshBuildTimeMs, but for the case when the X-Ray mode is disabled. It takes much longer to build
  // the meshes, but as long as they are not needed, we can take our time. It mostly affects the moments after the game
  // loading since this mode is disabled by default.
  const int MaxChunkMeshBuildTimeInactiveMs = 5;

  // Chunk size directly affects rebuild cost. The incremental cost goes down on the smaller chunks. However, the
  // smaller chunks take more time on full map rebuild. The full rebuild happens when the current show mode is changed
  // or the wireframe type is changed to wireframe (any type). 
  //
  // Another limiter is the total number of vertices in the contour mesh. Unity has a limit of 65k vertices per mesh. 
  // Approximate worst-case vertex counts for contour rendering (checkerboard-like terrain with maximum contour
  // complexity):
  //
  //  16x16 -> ~3k vertices.
  //  32x32 -> ~12k vertices.
  //  64x64 -> ~49k vertices.
  //
  //  Anything larger will be dangerously close to the limit.
  const int XMeshSize = 16;
  const int YMeshSize = 16;

  #region Implementation of IPostLoadableSingleton

  /// <inheritdoc/>
  public void PostLoad() {
    _root = rootObjectProvider.CreateRootObject("XRayWireframeTerrainMesh");
    terrainMap.TerrainAdded += OnTerrainAdded;
    terrainMap.TerrainRemoved += OnTerrainRemoved;
    colorSettings.WireframeModeInternal.ValueChanged += (_, _) => {
      SetMode(Enum.Parse<Mode>(colorSettings.WireframeModeInternal.Value));
    };
    colorSettings.WireframeEdgeColor.ValueChanged += (_, _) => {
      SetEdgesColor(colorSettings.WireframeEdgeColor.Color);
    };
    _currentMode = Enum.Parse<Mode>(colorSettings.WireframeModeInternal.Value);
    BuildMeshEdgeCache();
  }

  #endregion

  #region ILateUpdatableSingleton implementation

  public void LateUpdateSingleton() {
    if (!_needUpdate && _dirtyChunks.Count == 0) {
      return;
    }
    _needUpdate = false;

    if (_currentMode == Mode.None) {
      _dirtyChunks.Clear();
      DestroyWireOverlay();
      return;
    }
    if (!_overlay) {
      _dirtyChunks.Clear();
      _overlay = CreateWireOverlay();
      _overlay.transform.SetParent(_root.transform, false);
    }
    _overlay.SetActive(IsActive);

    RebuildDirtyChunkMeshes();
  }

  #endregion

  #region API

  /// <summary>Indicates whether the overlay is currently visible.</summary>
  /// <seealso cref="Activate"/>
  /// <seaalso cref="Deactivate"/>
  public bool IsActive { get; private set; }

  /// <summary>Controls how terrain geometry is visualized.</summary>
  public enum Mode {
    // No wireframe at all.
    None,
    // Only the tile edges where the height changes are outlined.
    Contours,
    // Every tile edge is outlined. Except the underground ones.
    Grid,
  }

  /// <summary>Shows the overlay.</summary>
  /// <remarks>
  /// The mesh cache is maintained independently from visibility and is normally already up-to-date when
  /// this method is called.
  /// </remarks>
  /// <seealso cref="IsActive"/>
  public void Activate() {
    IsActive = true;
    _needUpdate = true;
  }

  /// <summary>Hides the overlay.</summary>
  /// <remarks>
  /// The cached meshes are preserved and continue to track terrain changes while hidden.
  /// </remarks>
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
  Mode _currentMode = Mode.None;
  bool _needUpdate;

  enum FaceDirection {
    Top,
    Bottom,
    West,
    East,
    South,
    North,
  }

  readonly record struct Face(FaceDirection Direction, Vector3Int Offset);

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

  static readonly Face[] Faces = [
      new(FaceDirection.Top, new Vector3Int(0, 0, 1)),
      new(FaceDirection.Bottom, new Vector3Int(0, 0, -1)),
      new(FaceDirection.West, new Vector3Int(-1, 0, 0)),
      new(FaceDirection.East, new Vector3Int(1, 0, 0)),
      new(FaceDirection.South, new Vector3Int(0, -1, 0)),
      new(FaceDirection.North, new Vector3Int(0, 1, 0)),
  ];

  readonly Dictionary<EdgeKey, EdgeInfo> _edgeCache = new();
  readonly Dictionary<Vector2Int, MeshFilter> _chunkMeshFilters = new();
  readonly HashSet<Vector2Int> _dirtyChunks = [];

  /// <summary>Changes the visualization mode.</summary>
  /// <remarks>
  /// Switching modes requires rebuilding the mesh representation because contour and grid modes use
  /// different edge filtering rules.
  /// </remarks>
  void SetMode(Mode mode) {
    if (_currentMode == mode) {
      return;
    }
    _currentMode = mode;
    _dirtyChunks.Clear();
    DestroyWireOverlay();
    _needUpdate = true;
  }

  void SetEdgesColor(Color color) {
    if (_material) {
      rendererFactory.SetMaterialColor(_material, color);
    }
  }

  GameObject CreateWireOverlay() {
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
        _dirtyChunks.Add(chunk);

        var mr = meshObj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
      }
    }
    _needUpdate = true;
    return overlayObj;
  }

  void DestroyWireOverlay() {
    Object.Destroy(_overlay);
    _overlay = null;
    Object.Destroy(_material);
    _material = null;
    _chunkMeshFilters.Clear();
  }

  /// <summary>Rebuilds the global edge cache from the current terrain state.</summary>
  /// <remarks>This is an expensive operation intended for initialization and full cache rebuilds.</remarks>
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

  /// <summary>Rebuilds mesh for dirty chunks, processing them in batches to avoid long update times.</summary>
  void RebuildDirtyChunkMeshes() {
    var stopWatch = Stopwatch.StartNew();
    var processedChunks = new List<Vector2Int>();
    foreach (var chunk in _dirtyChunks) {
      processedChunks.Add(chunk);
      if (!_chunkMeshFilters.TryGetValue(chunk, out var mf)) {
        continue;
      }
      if (mf.sharedMesh) {
        Object.Destroy(mf.sharedMesh);
      }
      mf.sharedMesh = BuildChunkMesh(chunk);
      var maxLatency = IsActive ? MaxChunkMeshBuildTimeMs : MaxChunkMeshBuildTimeInactiveMs;
      if (stopWatch.ElapsedMilliseconds > maxLatency) {
        break;
      }
    }
    if (processedChunks.Count == _dirtyChunks.Count) {
      _dirtyChunks.Clear();
    } else {
      _dirtyChunks.RemoveWhere(processedChunks.Contains);
    }
  }

  Mesh BuildChunkMesh(Vector2Int chunk) {
    var startX = chunk.x * XMeshSize;
    var startY = chunk.y * YMeshSize;
    var endX = Mathf.Min(startX + XMeshSize, mapSize.TerrainSize.x);
    var endY = Mathf.Min(startY + YMeshSize, mapSize.TerrainSize.y);

    var vertices = new List<Vector3>();
    var indices = new List<int>();
    foreach (var (edge, info) in _edgeCache) {
      if (!ShouldRenderEdge(info) || !BelongsToChunk(edge, startX, endX, startY, endY)) {
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

  /// <summary>Updates the mesh cache to reflect a terrain voxel change.</summary>
  /// <remarks>
  /// The update is applied incrementally and only affects chunks that contain modified contour edges.
  /// </remarks>
  void ApplyVoxelChange(Vector3Int coordinates, bool isSolid) {
    if (isSolid) {
      foreach (var face in Faces) {
        var neighbor = coordinates + face.Offset;
        if (IsSolid(neighbor)) {
          ChangeSingleFace(neighbor, Opposite(face.Direction), -1);
        }
      }
      ChangeVisibleCubeFaces(coordinates, +1);
    } else {
      ChangeVisibleCubeFaces(coordinates, -1);
      foreach (var face in Faces) {
        var neighbor = coordinates + face.Offset;
        if (IsSolid(neighbor)) {
          ChangeSingleFace(neighbor, Opposite(face.Direction), +1);
        }
      }
    }
    _needUpdate = true;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  bool IsSolid(Vector3Int coordinates) {
    return terrainMap.IsTerrainVoxel(coordinates);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void ChangeVisibleCubeFaces(Vector3Int coordinates, int delta) {
    foreach (var face in Faces) {
      if (!IsSolid(coordinates + face.Offset)) {
        ChangeSingleFace(coordinates, face.Direction, delta);
      }
    }
  }

  void ChangeSingleFace(Vector3Int coordinates, FaceDirection direction, int delta) {
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

    switch (direction) {
      case FaceDirection.Top:
        ChangeFaceEdges(p010, p110, p111, p011, direction, delta);
        break;
      case FaceDirection.Bottom:
        ChangeFaceEdges(p000, p001, p101, p100, direction, delta);
        break;
      case FaceDirection.West:
        ChangeFaceEdges(p000, p010, p011, p001, direction, delta);
        break;
      case FaceDirection.East:
        ChangeFaceEdges(p100, p101, p111, p110, direction, delta);
        break;
      case FaceDirection.South:
        ChangeFaceEdges(p000, p100, p110, p010, direction, delta);
        break;
      case FaceDirection.North:
        ChangeFaceEdges(p001, p011, p111, p101, direction, delta);
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  void ChangeFaceEdges(Vector3Int a, Vector3Int b, Vector3Int c, Vector3Int d, FaceDirection direction, int delta) {
    ChangeEdge(a, b, direction, delta);
    ChangeEdge(b, c, direction, delta);
    ChangeEdge(c, d, direction, delta);
    ChangeEdge(d, a, direction, delta);
  }

  void ChangeEdge(Vector3Int a, Vector3Int b, FaceDirection direction, int delta) {
    var key = EdgeKey.Create(a, b);
    _edgeCache.TryGetValue(key, out var info);
    var wasRendered = ShouldRenderEdge(info);
    AddNormal(ref info, direction, delta);
    if (info.Total <= 0) {
      _edgeCache.Remove(key);
    } else {
      _edgeCache[key] = info;
    }
    var isRendered = info.Total > 0 && ShouldRenderEdge(info);
    if (wasRendered != isRendered) {
      _needUpdate = true;
      _dirtyChunks.Add(GetOwnerChunk(key));
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  static void AddNormal(ref EdgeInfo info, FaceDirection direction, int delta) {
    info.Total += delta;
    switch (direction) {
      case FaceDirection.Top:
        info.Top += delta;
        break;
      case FaceDirection.Bottom:
        info.Bottom += delta;
        break;
      case FaceDirection.West:
        info.West += delta;
        break;
      case FaceDirection.East:
        info.East += delta;
        break;
      case FaceDirection.South:
        info.South += delta;
        break;
      case FaceDirection.North:
        info.North += delta;
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
    }
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
  bool ShouldRenderEdge(EdgeInfo info) {
    return _currentMode switch {
        Mode.Contours => IsContourEdge(info),
        Mode.Grid => info.Total > 0,
        _ => false,
    };
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

  static FaceDirection Opposite(FaceDirection direction) {
    return direction switch {
        FaceDirection.Top => FaceDirection.Bottom,
        FaceDirection.Bottom => FaceDirection.Top,
        FaceDirection.West => FaceDirection.East,
        FaceDirection.East => FaceDirection.West,
        FaceDirection.South => FaceDirection.North,
        FaceDirection.North => FaceDirection.South,
        _ => direction,
    };
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
    ApplyVoxelChange(coordinates, isSolid: true);
  }

  void OnTerrainRemoved(object sender, Vector3Int coordinates) {
    ApplyVoxelChange(coordinates, isSolid: false);
  }

  #endregion
}

// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.XRay.Patches;
using IgorZ.XRay.Settings;
using Timberborn.LevelVisibilitySystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystemRendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.XRay.Core;

class TransparentTerrainMeshService : IPostLoadableSingleton {

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    _meshSettings.GrassColor.ValueChanged += (_, _) => MakeMaterials(forceRefresh: true);
    _meshSettings.CliffColor.ValueChanged += (_, _) => MakeMaterials(forceRefresh: true);
    _meshSettings.CliffEdgeColor.ValueChanged += (_, _) => MakeMaterials(forceRefresh: true);
    _meshSettings.GhostModeIntensity.ValueChanged += (_, _) => MakeMaterials(forceRefresh: true);
    _meshSettings.GlowingEdges.ValueChanged += (_, _) => MakeMaterials(forceRefresh: true);
    _meshSettings.IgnoreVisibleLevel.ValueChanged += (_, _) => {
      if (IsActive) {
        InvalidateTerrainMeshes();
      }
    };
    _levelVisibilityService.MaxVisibleLevelChanged += OnMaxVisibleLevelChanged;
  }

  #endregion

  #region API

  /// <summary>Tells if the transparent mode is enabled.</summary>
  /// <remarks>
  /// In this mod, the stock game material settings of the terrain meshes are overriden. If the meshes get changed, this
  /// is intercepted and the mesh is reset to the transparent settings. This applies a minor performance overhead. When
  /// the mode is disabled, there is no overhead at all as nothing is being done.
  /// </remarks>
  /// <seealso cref="TileComponentsPatch"/>
  public bool IsActive { get; private set; }

  /// <summary>Activates the transparent terrain mesh mode.</summary>
  /// <seealso cref="IsActive"/>
  public void Activate() {
    if (IsActive) {
      return;
    }
    MakeMaterials();
    IsActive = true;
    InvalidateTerrainMeshes();
    var renderers = GetTerrainRenderers();
    foreach (var renderer in renderers) {
      SetXRayRenderer(renderer);
    }
  }

  /// <summary>Deactivates the transparent terrain mesh mode.</summary>
  /// <seealso cref="IsActive"/>
  public void Deactivate() {
    if (!IsActive) {
      return;
    }
    IsActive = false;
    var renderers = GetTerrainRenderers(includeInactive: true);
    foreach (var renderer in renderers) {
      SetOriginalRenderer(renderer);
    }
    InvalidateTerrainMeshes();
  }

  /// <summary>Checks if the terrain voxel should be included in the current X-Ray terrain mesh.</summary>
  /// <remarks>Called from a Harmony patch while the game rebuilds terrain meshes.</remarks>
  internal bool ShouldRenderTerrainCoordinate(Vector3Int coordinates) {
    return !IsActive
        || _meshSettings.IgnoreVisibleLevel.Value
        || _levelVisibilityService.TerrainLevelIsAtMax
        || coordinates.z <= _levelVisibilityService.MaxVisibleLevel;
  }

  #endregion

  #region Implementation

  // The original terrain material names. They are checked for the exact match!
  const string OriginalGrassMaterialName = "Grass";
  const string OriginalCliffMaterialName = "Cliff";
  const string OriginalCliffEdgeMaterialName = "CliffEdge";

  // The names of our custom materials, which will replace the original once's.
  const string XRayGrassMaterialName = "XRay_Grass";
  const string XRayCliffMaterialName = "XRay_Cliff";
  const string XRayCliffEdgeMaterialName = "XRay_CliffEdge";

  ShadowCastingMode _originalShadowCastingMode;
  bool _originalReceiveShadows;

  Material _xrayGrassMaterial;
  Material _xrayCliffMaterial;
  Material _xrayCliffEdgeMaterial;
  Material _originalGrassMaterial;
  Material _originalCliffMaterial;
  Material _originalCliffEdgeMaterial;

  readonly RendererFactory _rendererFactory;
  readonly TerrainMeshManager _terrainMeshManager;
  readonly ILevelVisibilityService _levelVisibilityService;
  readonly MeshSettings _meshSettings;

  // Primarily made for the efficient patches handling.
  internal static TransparentTerrainMeshService Instance { get; private set; }

  TransparentTerrainMeshService(
      RendererFactory rendererFactory, TerrainMeshManager terrainMeshManager,
      ILevelVisibilityService levelVisibilityService, MeshSettings meshSettings) {
    Instance = this;
    _rendererFactory = rendererFactory;
    _terrainMeshManager = terrainMeshManager;
    _levelVisibilityService = levelVisibilityService;
    _meshSettings = meshSettings;
  }

  /// <summary>Sets the X-Ray material(s) to the renderer and also can remember the original material.</summary>
  /// <remarks>
  /// This method expects very specific material names to be present. Only if the original material is found, it will be
  /// replaced (and recorded for the restore!). The unrecognized materials will not be affected.
  /// </remarks>
  /// <seealso cref="SetOriginalRenderer"/>
  internal void SetXRayRenderer(Renderer renderer) {
    _originalShadowCastingMode = renderer.shadowCastingMode;
    _originalReceiveShadows = renderer.receiveShadows;
    var newMaterials = new Material[renderer.sharedMaterials.Length];
    for (var i = 0; i < renderer.sharedMaterials.Length; i++) {
      switch (renderer.sharedMaterials[i].name) {
        case OriginalCliffMaterialName:
          _originalCliffMaterial = renderer.sharedMaterials[i];
          newMaterials[i] = _xrayCliffMaterial;
          break;
        case OriginalCliffEdgeMaterialName:
          _originalCliffEdgeMaterial = renderer.sharedMaterials[i];
          newMaterials[i] = _xrayCliffEdgeMaterial;
          break;
        case OriginalGrassMaterialName:
          _originalGrassMaterial = renderer.sharedMaterials[i];
          newMaterials[i] = _xrayGrassMaterial;
          break;
        default:
          newMaterials[i] = renderer.sharedMaterials[i];
          break;
      }
    }
    renderer.sharedMaterials = newMaterials;
    renderer.shadowCastingMode = ShadowCastingMode.Off;
    renderer.receiveShadows = false;
  }

  /// <summary>Restores the original (game) materials on teh renderer.</summary>
  /// <remarks>
  /// The original materials must be saved first! It is done in <seealso cref="SetXRayRenderer"/>. Which gets called via
  /// a patch on any meshes update. In theory, it should ensure we always have the original material when we need to
  /// restore. 
  /// </remarks>
  /// <seealso cref="SetXRayRenderer"/>
  void SetOriginalRenderer(Renderer renderer) {
    var newMaterials = new Material[renderer.sharedMaterials.Length];
    for (var i = 0; i < renderer.sharedMaterials.Length; i++) {
      newMaterials[i] = renderer.sharedMaterials[i].name switch {
          XRayCliffMaterialName => _originalCliffMaterial,
          XRayCliffEdgeMaterialName => _originalCliffEdgeMaterial,
          XRayGrassMaterialName => _originalGrassMaterial,
          _ => renderer.sharedMaterials[i],
      };
    }
    renderer.sharedMaterials = newMaterials;
    renderer.shadowCastingMode = _originalShadowCastingMode;
    renderer.receiveShadows = _originalReceiveShadows;
  }

  /// <summary>Returns all the active renderers for the terrain meshes.</summary>
  List<Renderer> GetTerrainRenderers(bool includeInactive = false) {
    return _terrainMeshManager._tiles.Values
        .Select(tileComponents => tileComponents._meshRenderer)
        .Where(renderer => renderer && renderer.sharedMaterials != null
            && (includeInactive || renderer.gameObject.activeSelf))
        .Cast<Renderer>()
        .ToList();
  }

  void OnMaxVisibleLevelChanged(object sender, int maxVisibleLevel) {
    if (IsActive) {
      InvalidateTerrainMeshes();
    }
  }

  void InvalidateTerrainMeshes() {
    foreach (var tileIndex in _terrainMeshManager._tiles.Keys) {
      _terrainMeshManager._invalidTiles.Add(tileIndex);
    }
    // TerrainMeshManager processes invalid tiles only after at least one dirty surface-shape code is present.
    _terrainMeshManager._dirtyCodes.Add(Vector3Int.zero);
  }

  /// <summary>Makes or refreshes the materials for the "ghost" meshes.</summary>
  void MakeMaterials(bool forceRefresh = false) {
    if (!forceRefresh && _xrayGrassMaterial && _xrayCliffMaterial && _xrayCliffEdgeMaterial) {
      return;
    }
    _xrayGrassMaterial = GetTransparencyShader(XRayGrassMaterialName);
    _xrayCliffMaterial = GetTransparencyShader(XRayCliffMaterialName);
    _xrayCliffEdgeMaterial = GetTransparencyShader(XRayCliffEdgeMaterialName);

    if (!IsActive) {
      return;
    }
    //FIXME: separate create and set color/settings
    var renderers = GetTerrainRenderers();
    foreach (var renderer in renderers) {
      var materials = renderer.sharedMaterials;
      for (var i = 0; i < materials.Length; i++) {
        materials[i] = materials[i].name switch {
            XRayGrassMaterialName => _xrayGrassMaterial,
            XRayCliffMaterialName => _xrayCliffMaterial,
            XRayCliffEdgeMaterialName => _xrayCliffEdgeMaterial,
            _ => materials[i],
        };
      }
      renderer.sharedMaterials = materials;
    }
  }

  /// <summary>Creates a shader and the material to be represented as "transparent".</summary>
  /// <remarks>This shader will run after the water shader. It's required.</remarks>
  /// <param name="name">One of the "XRay*" material names. The list is strictly limited!</param>
  /// <seealso cref="XRayGrassMaterialName"/>
  /// <seealso cref="XRayCliffMaterialName"/>
  /// <seealso cref="XRayCliffEdgeMaterialName"/>
  // ReSharper disable Unity.PreferAddressByIdToGraphicsParams
  Material GetTransparencyShader(string name) {
    var referenceColor = name switch {
        XRayGrassMaterialName => _meshSettings.GrassColor.Color,
        XRayCliffMaterialName => _meshSettings.CliffColor.Color,
        XRayCliffEdgeMaterialName => _meshSettings.CliffEdgeColor.Color,
        _ => throw new InvalidOperationException($"Unexpected material name: {name}"),
    };
    var transparency = _meshSettings.GhostModeIntensity.Value / 100f;
    var color = _meshSettings.GlowingEdges.Value
        ? referenceColor * transparency
        : new Color(referenceColor.r, referenceColor.g, referenceColor.b, transparency);
    var mat = _rendererFactory.CreateTransparencyMaterial(name, color);
    if (_meshSettings.GlowingEdges.Value) {
      _rendererFactory.SetMaterialToGlowing(mat);
    }
    return mat;
  }

  #endregion
}

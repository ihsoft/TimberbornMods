// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.XRay.Patches;
using IgorZ.XRay.Settings;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystemRendering;
using Timberborn.WaterSystemRendering;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.XRay.Core;

sealed class XRayService(TerrainMeshManager terrainMeshManager, IWaterMesh waterMesh) : ILoadableSingleton {

  // Used to render the X-Ray models.
  const string UnlitShaderName = "Universal Render Pipeline/Unlit";

  // Used to determine the right render queue of the X-Ray models.
  const string WaterMaterialName = "PhysicalWater_Opaque";

  // The render queue value to use as a fallback if WaterMaterialName was not detected.
  const int DefaultWaterRednerQueue = 3000;  // As of Timberborn v1.0.13

  // The original terrain material names. They are checked for the exact match!
  const string OriginalGrassMaterialName = "Grass";
  const string OriginalCliffMaterialName = "Cliff";
  const string OriginalCliffEdgeMaterialName = "CliffEdge";

  public bool IsActive { get; private set; }

  public void SetActiveMode(bool state) {
    if (state == IsActive) {
      return;
    }
    if (state) {
      SetXRayMode();
    } else {
      ResetXRayMode();
    }
  }

  #region ILoadableSingleton implementation

  public void Load() {
    MakeMaterials();
    ColorSettings.OnSettingsUpdated = MakeMaterials; 
  }

  #endregion

  #region Implementation

  // The names of our custom materials, which will repace the original once's.
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

  // This value is set once per the game launch.
  static int _waterRenderQueue = -1;

  void SetXRayMode() {
    if (_waterRenderQueue == -1) {
      _waterRenderQueue = DetectWaterRenderQueue();
    }
    
    var renderers = GetTerrainRenderers();
    DebugEx.Info("Enable X-Ray mode: meshes={0}", renderers.Count);
    IsActive = true;
    TileComponentsPatch.FixRenderer = SetXRayRenderer;
    foreach (var renderer in renderers) {
      SetXRayRenderer(renderer);
    }
  }

  void ResetXRayMode() {
    var renderers = GetTerrainRenderers();
    DebugEx.Info("Disable X-Ray mode: meshes={0}", renderers.Count);
    IsActive = false;
    TileComponentsPatch.FixRenderer = null;
    foreach (var renderer in renderers) {
      SetOriginalRenderer(renderer);
    }
  }

  /// <summary>Sets the X-Ray material(s) to the renderer and also can remember the original material.</summary>
  /// <remarks>
  /// This method expects very specific material names to be present. Only if the original material is found, it will be
  /// replaced (and recorded for the restore!). The unrecognized materials will not be affected.
  /// </remarks>
  /// <seealso cref="SetOriginalRenderer"/>
  void SetXRayRenderer(Renderer renderer) {
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

  /// <summary>Returns all teh active renderers for the terrain meshes.</summary>
  List<Renderer> GetTerrainRenderers() {
    return terrainMeshManager._tiles.Values
        .Select(tileComponents => tileComponents._meshRenderer)
        .Where(renderer => renderer && renderer.sharedMaterials != null && renderer.gameObject.activeSelf)
        .Cast<Renderer>()
        .ToList();
  }

  /// <summary>Makes or refreshes the materials for the "ghost" meshes.</summary>
  void MakeMaterials() {
    _xrayGrassMaterial = GetTransparencyShader(XRayGrassMaterialName);
    _xrayCliffMaterial = GetTransparencyShader(XRayCliffMaterialName);
    _xrayCliffEdgeMaterial = GetTransparencyShader(XRayCliffEdgeMaterialName);

    if (!IsActive) {
      return;
    }
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
  static Material GetTransparencyShader(string name) {
    var mat = new Material(Shader.Find(UnlitShaderName)) {
        name = name,
        renderQueue = _waterRenderQueue + 1, // Just after water since we need it to get rendered first.
    };

    mat.SetOverrideTag("RenderType", "Transparent");

    mat.SetFloat("_Surface", 1); // Transparent
    mat.SetFloat("_Blend", 0);   // Alpha
    mat.SetFloat("_ZWrite", 0);  // Don't hide underground objects
    mat.SetFloat("_Cull", (float)CullMode.Back);

    mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
    mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
    mat.SetInt("_SrcBlendAlpha", (int)BlendMode.One);
    mat.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);

    mat.DisableKeyword("_ALPHATEST_ON");
    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    mat.DisableKeyword("_ALPHAMODULATE_ON");
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

    if (ColorSettings.GlowingEdges) {
      if (name == XRayCliffEdgeMaterialName) {
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
      } else {
        mat.SetInt("_SrcBlend", (int)BlendMode.One);
      }
      mat.SetInt("_DstBlend", (int)BlendMode.One);
    }

    Color color;
    if (ColorSettings.GlowingEdges) {
      color = name switch {
          XRayGrassMaterialName => ColorSettings.GlowGrassColor,
          XRayCliffMaterialName => ColorSettings.GlowCliffColor,
          XRayCliffEdgeMaterialName => ColorSettings.GlowCliffEdgeColor,
          _ => throw new InvalidOperationException($"Unexpected material name: {name}"),
      };
    } else {
      color = name switch {
          XRayGrassMaterialName => ColorSettings.GrassColor,
          XRayCliffMaterialName => ColorSettings.CliffColor,
          XRayCliffEdgeMaterialName => ColorSettings.CliffEdgeColor,
          _ => throw new InvalidOperationException($"Unexpected material name: {name}"),
      };
    }
    mat.SetColor("_BaseColor", color);
    mat.SetColor("_Color",     color);

    return mat;
  }
  // ReSharper restore Unity.PreferAddressByIdToGraphicsParams

  /// <summary>Detects the render queue which the water render pipeline uses.</summary>
  /// <remarks>The "ghost" models needs to be drawn after all the vital objects have been rendered.</remarks>
  int DetectWaterRenderQueue() {
    if (waterMesh is not WaterMesh waterMeshObj) {
      DebugEx.Warning("Cannot get WaterMesh. Defaulting water render queue to {0}", DefaultWaterRednerQueue);
      return DefaultWaterRednerQueue;
    }
    var renderQueue = -1;
    foreach (var renderer in waterMeshObj._waterTiles.GetComponentsInChildren<Renderer>()) {
      var material = renderer.sharedMaterials.FirstOrDefault(x => x.name == WaterMaterialName);
      if (material != null) {
         renderQueue = material.renderQueue;
         break;
      }
    }
    if (renderQueue == -1) {
      DebugEx.Warning("Got WaterMesh, but cannot detect render queue. Using default: {0}", DefaultWaterRednerQueue);
      return DefaultWaterRednerQueue;
    }
    return renderQueue;
  }

  #endregion
}

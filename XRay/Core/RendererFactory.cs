// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using Timberborn.WaterSystemRendering;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.Rendering;

namespace IgorZ.XRay.Core;

sealed class RendererFactory(IWaterMesh waterMesh) {

  static readonly int SrcBlendProperty = Shader.PropertyToID("_SrcBlend");
  static readonly int DstBlendProperty = Shader.PropertyToID("_DstBlend");
  static readonly int SrcBlendAlphaProperty = Shader.PropertyToID("_SrcBlendAlpha");
  static readonly int DstBlendAlphaProperty = Shader.PropertyToID("_DstBlendAlpha");
  static readonly int SurfaceProperty = Shader.PropertyToID("_Surface");
  static readonly int BlendProperty = Shader.PropertyToID("_Blend");
  static readonly int ZWriteProperty = Shader.PropertyToID("_ZWrite");
  static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
  static readonly int ColorProperty = Shader.PropertyToID("_Color");

  #region API

  /// <summary>
  /// The base water renderer queue. Use it to ensure the effects are drawn after the water is rendered, but before the
  /// UI stuff is made.
  /// </summary>
  public int WaterRendererQueue => _waterRendererQueue ??= DetectWaterRenderQueue();
  static int? _waterRendererQueue;  // Need to detect only once per game load.

  /// <summary>Creates a transparency material with the specified name and color.</summary>
  public Material CreateTransparencyMaterial(string name, Color color) {
    var mat = new Material(Shader.Find(UrpUnlitShaderName)) {
        name = name,
        renderQueue = WaterRendererQueue + 1, // Just after water since we need it to get rendered first.
    };

    mat.SetInt(SrcBlendProperty, (int)BlendMode.SrcAlpha);
    mat.SetInt(DstBlendProperty, (int)BlendMode.OneMinusSrcAlpha);
    mat.SetInt(SrcBlendAlphaProperty, (int)BlendMode.One);
    mat.SetInt(DstBlendAlphaProperty, (int)BlendMode.OneMinusSrcAlpha);

    mat.SetOverrideTag("RenderType", "Transparent");
    mat.SetFloat(SurfaceProperty, 1); // Transparent
    mat.SetFloat(BlendProperty, 0);   // Alpha
    mat.SetFloat(ZWriteProperty, 0);

    mat.SetColor(BaseColorProperty, color);
    mat.SetColor(ColorProperty, color);

    return mat;
  }

  /// <summary>Makes the material glowing instead of blending by the alpha channel.</summary>
  public void SetMaterialToGlowing(Material mat) {
    mat.SetInt(SrcBlendProperty, (int)BlendMode.One);
    mat.SetInt(DstBlendProperty, (int)BlendMode.One);
  }

  #endregion

  #region Implementation

  // Normal shader that supports transparency.
  const string UrpUnlitShaderName = "Universal Render Pipeline/Unlit";

  // Used to determine the right render queue of the X-Ray models.
  const string WaterMaterialName = "PhysicalWater_Opaque";

  // The render queue value to use as a fallback if WaterMaterialName was not detected.
  const int DefaultWaterRednerQueue = 3000;  // As of Timberborn v1.0.13

  /// <summary>Detects the render queue which the water render pipeline uses.</summary>
  /// <remarks>The "ghost" models need to be drawn after all the vital objects have been rendered.</remarks>
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

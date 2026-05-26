// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.XRay.Core;

sealed class XRayService {

  public static XRayService Instance { get; private set; }
  public bool IsActive { get; private set; }

  public void SetActiveMode(bool state) {
    if (state == IsActive) {
      return;
    }
    IsActive = state;
    if (state) {
      SetXRayMode();
    } else {
      ResetXRayMode();
    }
  }

  #region Implementation

  readonly TransparentTerrainMeshService _transparentTerrainMeshService;

  XRayService(TerrainMeshManager terrainMeshManager, IWaterMesh waterMesh, ColorSettings colorSettings) {
  XRayService(TransparentTerrainMeshService transparentTerrainMeshService) {
    Instance = this;
    _transparentTerrainMeshService = transparentTerrainMeshService;
  }

  void SetXRayMode() {
    DebugEx.Info("Enable X-Ray mode");
    _wireframeTerrainMeshService.EnableWireframe();
  }

  void ResetXRayMode() {
    DebugEx.Info("Disable X-Ray mode");
    _wireframeTerrainMeshService.DisableWireframe();
  }

  #endregion
}

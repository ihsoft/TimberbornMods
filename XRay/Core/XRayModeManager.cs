// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.XRay.Patches;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.XRay.Core;

sealed class XRayModeManager {

  #region API

  /// <summary>Tells if X-Ray mode is active.</summary>
  /// <remarks>
  /// In this mode, the buildings and cavers under the surface are shown. The selection tools try to pick up the
  /// locations under the surface. The surface hits are used as a fallback. This may apply some performance impact.
  /// </remarks>
  /// <seealso cref="SelectableObjectRaycasterPatch"/>
  /// <seealso cref="BlockObjectPreviewPickerPatch"/>
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

  #endregion

  #region Implementation

  readonly TransparentTerrainMeshService _transparentTerrainMeshService;

  // Primarily made for the efficient patches handling.
  internal static XRayModeManager Instance { get; private set; }

  XRayModeManager(TransparentTerrainMeshService transparentTerrainMeshService) {
    Instance = this;
    _transparentTerrainMeshService = transparentTerrainMeshService;
  }

  void SetXRayMode() {
    DebugEx.Info("Enable X-Ray mode");
    _transparentTerrainMeshService.Activate();
  }

  void ResetXRayMode() {
    DebugEx.Info("Disable X-Ray mode");
    _transparentTerrainMeshService.Deactivate();
  }

  #endregion
}

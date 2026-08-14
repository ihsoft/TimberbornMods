// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.XRay.Patches;

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

  public bool PassThroughSurfaceObjects =>
      IsActive && _transparentBuildingModelService.PassThroughSurfaceObjects;

  public void SetObjectTransparencyRequested(bool requested) {
    _objectTransparencyRequested = requested;
    ApplyObjectTransparency();
  }

  public void SynchronizeObjectTransparency() {
    if (_objectTransparencySynchronizationPending) {
      _objectTransparencySynchronizationPending = false;
      return;
    }
    ApplyObjectTransparency();
  }

  void ApplyObjectTransparency() {
    var active = IsActive && _objectTransparencyRequested;
    _transparentBuildingModelService.SetActive(active);
    _transparentNaturalResourceModelService.SetActive(active);
  }

  public void SetActiveMode(bool state) {
    if (state == IsActive) {
      return;
    }
    IsActive = state;
    _objectTransparencySynchronizationPending = true;
    if (state) {
      SetXRayMode();
    } else {
      ResetXRayMode();
    }
  }

  #endregion

  #region Implementation

  readonly TransparentTerrainMeshService _transparentTerrainMeshService;
  readonly TransparentBuildingModelService _transparentBuildingModelService;
  readonly TransparentNaturalResourceModelService _transparentNaturalResourceModelService;
  readonly WireframeTerrainMeshService _wireframeTerrainMeshService;
  bool _objectTransparencyRequested;
  bool _objectTransparencySynchronizationPending;

  // Primarily made for the efficient patches handling.
  internal static XRayModeManager Instance { get; private set; }

  XRayModeManager(
      TransparentTerrainMeshService transparentTerrainMeshService,
      TransparentBuildingModelService transparentBuildingModelService,
      TransparentNaturalResourceModelService transparentNaturalResourceModelService,
      WireframeTerrainMeshService wireframeTerrainMeshService) {
    Instance = this;
    _transparentTerrainMeshService = transparentTerrainMeshService;
    _transparentBuildingModelService = transparentBuildingModelService;
    _transparentNaturalResourceModelService = transparentNaturalResourceModelService;
    _wireframeTerrainMeshService = wireframeTerrainMeshService;
  }

  void SetXRayMode() {
    _transparentTerrainMeshService.Activate();
    _wireframeTerrainMeshService.Activate();
  }

  void ResetXRayMode() {
    _transparentTerrainMeshService.Deactivate();
    _wireframeTerrainMeshService.Deactivate();
  }

  #endregion
}

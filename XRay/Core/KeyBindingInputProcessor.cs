// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.XRay.Core;

sealed class KeyBindingInputProcessor(XRayModeManager xRayModeManager, InputService inputService)
    : IPostLoadableSingleton, IInputProcessor {

  internal const string ToggleModeBindingKey = "IgorZ-XRayToggleMode"; // Handled by the mode panel.
  internal const string ShowModeBindingKey = "IgorZ-XRayShow";
  internal const string ToggleObjectTransparencyBindingKey = "IgorZ-XRayToggleObjectTransparency";

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    inputService.AddInputProcessor(this);
  }

  #endregion

  #region IInputProcessor implementation

  /// <inheritdoc/>
  public bool ProcessInput() {
    xRayModeManager.SynchronizeObjectTransparency();
    if (!xRayModeManager.IsActive) {
      _objectTransparencyKeyPressedInXRay = false;
    } else if (inputService.IsKeyDown(ToggleObjectTransparencyBindingKey)) {
      _objectTransparencyKeyPressedInXRay = true;
      xRayModeManager.SetObjectTransparencyRequested(true);
    }
    if (_objectTransparencyKeyPressedInXRay && inputService.IsKeyUp(ToggleObjectTransparencyBindingKey)) {
      _objectTransparencyKeyPressedInXRay = false;
      if (inputService.IsKeyUpAfterShortHeld(ToggleObjectTransparencyBindingKey)) {
        _objectTransparencyToggled = !_objectTransparencyToggled;
      }
      xRayModeManager.SetObjectTransparencyRequested(_objectTransparencyToggled);
    }
    var newShowMode = inputService.IsKeyHeld(ShowModeBindingKey);
    if (_xrayModeKeyHeld != newShowMode && (!newShowMode || !xRayModeManager.IsActive)) {
      _xrayModeKeyHeld = newShowMode;
      xRayModeManager.SetActiveMode(newShowMode);
    }
    return false;
  }
  bool _xrayModeKeyHeld;
  bool _objectTransparencyToggled;
  bool _objectTransparencyKeyPressedInXRay;

  #endregion
}

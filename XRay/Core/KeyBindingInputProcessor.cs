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

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    inputService.AddInputProcessor(this);
  }

  #endregion

  #region IInputProcessor implementation

  /// <inheritdoc/>
  public bool ProcessInput() {
    var newShowMode = inputService.IsKeyHeld(ShowModeBindingKey);
    if (_xrayModeKeyHeld != newShowMode && (!newShowMode || !xRayModeManager.IsActive)) {
      _xrayModeKeyHeld = newShowMode;
      xRayModeManager.SetActiveMode(newShowMode);
    }
    return false;
  }
  bool _xrayModeKeyHeld;

  #endregion
}

// Timberborn Mod: X-Ray
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.InputSystem;
using Timberborn.SingletonSystem;

namespace IgorZ.XRay.Core;

sealed class KeyBindingInputProcessor(XRayService xRayService, InputService inputService)
    : IPostLoadableSingleton, IInputProcessor {

  const string ToggleModeKeyBindingId = "IgorZ-XRayToggleMode";

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    inputService.AddInputProcessor(this);
  }

  #endregion

  #region IInputProcessor implementation

  /// <inheritdoc/>
  public bool ProcessInput() {
    if (inputService.IsKeyDown(ToggleModeKeyBindingId)) {
      xRayService.SetActiveMode(!xRayService.IsActive);
      return true;
    }
    return false;
  }

  #endregion
}

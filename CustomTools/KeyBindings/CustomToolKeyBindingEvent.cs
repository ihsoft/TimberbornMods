// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.KeyBindingSystem;

namespace IgorZ.CustomTools.KeyBindings;

/// <summary>The event that is fired when key binding press detected.</summary>
/// <remarks>
/// This event is only triggered for the bindings with empty <see cref="CustomToolBindingSpec"/>. The listener should
/// consume the keybinding via <see cref="KeyBindingInputProcessor.ConsumeKeyBinding"/>.
/// </remarks>
public record CustomToolKeyBindingEvent {
  /// <summary>The binding that was pressed.</summary>
  public KeyBinding KeyBinding { get; init; }

  /// <summary>The custom binding spec of the binding.</summary>
  public CustomToolBindingSpec CustomToolBindingSpec  { get; init; }
}

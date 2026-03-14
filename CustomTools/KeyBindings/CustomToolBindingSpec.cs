// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;
using Timberborn.ToolSystem;

namespace IgorZ.CustomTools.KeyBindings;

/// <summary>Specification for custom tools key bindings.</summary>
/// <remarks>
/// If no type or blueprint are provided, then the keybinding activation will result in event
/// <see cref="CustomToolKeyBindingEvent"/> posted to the event bus.
/// </remarks>
public record CustomToolBindingSpec : ComponentSpec {
  /// <summary>Optional. The full name of the class type that implements <see cref="ITool"/>.</summary>
  /// <remarks>This class must be instantiable via Bindito.</remarks>
  [Serialize]
  public string Type { get; init; }

  /// <summary>Optional. The blueprint that a blockobject tool places.</summary>
  [Serialize]
  public string BlockObjectBlueprint { get; init; }

  /// <summary>Optional. The ID of a custom tool to activate.</summary>
  [Serialize]
  public string CustomToolId { get; init; }
}

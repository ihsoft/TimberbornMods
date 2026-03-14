// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.CustomTools.Core;

/// <summary>Extra data about the custom groups.</summary>
public record CustomToolGroupSpec : ComponentSpec {
  /// <summary>Color style of the group.</summary>
  /// <remarks>Values: "red", "blue", "green". Case-insensitive.</remarks>
  [Serialize]
  public string Style { get; init; } = "Blue";

  /// <summary>The tool group button order in case of there are multiple groups in the layout.</summary>
  /// <remarks>
  /// <p>
  /// Used to order group buttons in case of there are multiple in the same layout. Duplicates are allowed. The ordering
  /// is maintained only for the CustomTool groups.
  /// </p>
  /// <p>
  /// The actual location of the new buttons depends on the layout. For the "left" and "right" layouts, the buttons are
  /// added at the end of the stock buttons list. For the "middle" layout, the buttons are inserted at the beginning
  /// (middle-left).
  /// </p>
  /// </remarks>
  [Serialize]
  public int Order { get; init; }

  /// <summary>The tool's group position in the bottom bar.</summary>
  /// <remarks>Only applicable to the root groups. Values: "left", "middle", "right". Case-insensitive.</remarks>
  [Serialize]
  public string Layout { get; init; }

  /// <summary>The optional parent group. If not provided, then it's a root group.</summary>
  /// <remarks>The group order will be used to position the button in the parent group.</remarks>
  [Serialize]
  public string ParentGroupId { get; init; }
}

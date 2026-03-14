// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.CustomTools.Tools;
using Timberborn.BlueprintSystem;
using Timberborn.ToolSystem;
using UnityEngine;

namespace IgorZ.CustomTools.Core;

/// <summary>Custom tool specification.</summary>
public record CustomToolSpec : ComponentSpec {
  /// <summary>required. The tool unique ID.</summary>
  [Serialize]
  public string Id { get; init; }

  /// <summary>The group to attach this tool to.</summary>
  /// <remarks>This should be a standard group, defined via <see cref="ToolGroupSpec"/></remarks>
  [Serialize]
  public string GroupId { get; init; }

  /// <summary>The full name of the class type that implements <see cref="AbstractCustomTool"/>.</summary>
  /// <remarks>This class must be instantiable via Bindito. It will be used to serve the tool actions.</remarks>
  [Serialize]
  public string Type { get; init; }

  /// <summary>The value to use to sort the tools within the group.</summary>
  /// <remarks>
  /// The tools with less order value go first. When adding tool to the game's stock groups, the tools are always added
  /// at the end of the stock list, but within the custom tools the order will be maintained. Duplicates are allowed,
  /// but the order will be undetermined in this case. 
  /// </remarks>
  [Serialize]
  public int Order { get; init; }

  /// <summary>Path to the icon to use for the tool.</summary>
  [Serialize]
  public AssetRef<Sprite> Icon { get; init; }

  /// <summary>LocKey string for the main tool caption.</summary>
  [Serialize]
  public string DisplayNameLocKey { get; init; }

  /// <summary>LocKey string for the tool description.</summary>
  [Serialize]
  public string DescriptionLocKey { get; init; }

  /// <summary>Specifies if this tool should only be visible in the DevMode.</summary>
  [Serialize]
  public bool DevMode { get; init; }
}
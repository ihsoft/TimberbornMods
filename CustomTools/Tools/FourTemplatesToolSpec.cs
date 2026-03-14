// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlueprintSystem;

namespace IgorZ.CustomTools.Tools;

/// <summary>Spec for the tool that can place up to FOUR buildings, based on the modifiers held.</summary>
/// <remarks>Use this spec to create a tool that can place multiple buildings.</remarks>
public record FourTemplatesToolSpec : ComponentSpec {
  /// <summary>The building template to apply if no modifiers are being held.</summary>
  [Serialize]
  public string NoModifierTemplate { get; init; }

  /// <summary>Optional. The building template to apply if SHIFT modifier is being held.</summary>
  [Serialize]
  public string ShiftModifierTemplate { get; init; }

  /// <summary>Optional. The building template to apply if CTRL modifier is being held.</summary>
  [Serialize]
  public string CtrlModifierTemplate { get; init; }

  /// <summary>Optional. The building template to apply if ALT modifier is being held.</summary>
  [Serialize]
  public string AltModifierTemplate { get; init; }

  /// <summary>Optional. Tells if the template suffix should be guessed.</summary>
  /// <remarks>
  /// By default, the template names are looked up "as-is", for the full name match. However, many templates have
  /// different names for different factions. If the only difference is the function name in the name suffix, set this
  /// property to <c>true</c>, and don't provide suffix in the setup. The mod will automatically pickup the current
  /// faction and add it to the name. Note that the original template name will always be tried first. If matched
  /// without suffix, then this version will be used.
  /// </remarks>
  [Serialize]
  public bool FactionNeutral { get; init; }
}

// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Tools;

/// <summary>Abstract tool that has description.</summary>
/// <remarks>
/// With no tweaking, this class simply uses values from the TimberAPI specification. If more advanced logic is needed,
/// then the descendants can add more sections. Dynamic descriptions are also supported.
/// </remarks>
public abstract class ToolWithDescription : CustomToolSystem.CustomTool {

  ToolDescription _cachedDescription;

  #region API

  /// <summary>
  /// The localization key of the text to present as the tool caption. If not overriden, then the string from the
  /// <see cref="CustomToolSystem.CustomTool.ToolSpec"/> is used.
  /// </summary>
  /// <remarks>If <c>null</c> or empty, then the title will not be shown.</remarks>
  protected virtual string DescriptionTitleLoc => ToolSpec?.NameLocKey;

  /// <summary>
  /// The localization key of the text to present as the tool description. If not overriden, then the string from the
  /// <see cref="CustomToolSystem.CustomTool.ToolSpec"/> is used.
  /// </summary>
  /// <remarks>If <c>null</c> or empty, then the description will not be shown.</remarks>
  protected virtual string DescriptionMainSectionLoc => ToolSpec?.DescriptionLocKey;

  /// <summary>The localization key of the optional text that is presented at the bottom of the main stuff.</summary>
  protected string DescriptionHintSectionLoc = null;

  /// <summary>
  /// The visual elements to add to via <see cref="ToolDescription.Builder.AddExternalSection"/>. It can be <c>null</c>.
  /// </summary>
  protected VisualElement[] DescriptionExternalSections = null;

  /// <summary>
  /// The visual elements to add to via <see cref="ToolDescription.Builder.AddSection(VisualElement)"/>.
  /// It can be <c>null</c>.
  /// </summary>
  protected VisualElement[] DescriptionVisualSections = null;

  /// <summary>Extra loc ID strings to add to the description as "bullets".</summary>
  /// <seealso cref="SpecialStrings.RowStarter"/>
  protected string[] DescriptionBullets = null;

  /// <summary>Forces the cached description to refresh. Call it when the description components have changed.</summary>
  protected void SetDescriptionDirty() {
    _cachedDescription = null;
  }

  /// <summary>Tells if any of the shift keys is held.</summary>
  protected bool IsShiftHeld => Keyboard.current.shiftKey.isPressed;

  /// <summary>Tells if any of the control keys is held.</summary>
  protected bool IsCtrlHeld => Keyboard.current.ctrlKey.isPressed;

  /// <summary>Tells if any of the alt keys is held.</summary>
  protected bool IsAltHeld => Keyboard.current.altKey.isPressed;

  #endregion

  #region Tool overrides

  /// <inheritdoc/>
  public override ToolDescription Description() {
    if (_cachedDescription != null) {
      return _cachedDescription;
    }
    var description =
        new ToolDescription.Builder(!string.IsNullOrEmpty(DescriptionTitleLoc) ? Loc.T(DescriptionTitleLoc) : null);
    var descriptionText = new StringBuilder();
    if (!string.IsNullOrEmpty(DescriptionMainSectionLoc)) {
      descriptionText.Append(Loc.T(DescriptionMainSectionLoc));
    }
    if (DescriptionBullets != null) {
      foreach (var descriptionBullet in DescriptionBullets) {
        descriptionText.Append("\n" + SpecialStrings.RowStarter + Loc.T(descriptionBullet));
      }
    }
    description.AddSection(descriptionText.ToString());
    if (DescriptionVisualSections != null) {
      foreach (var visualSection in DescriptionVisualSections) {
        description.AddSection(visualSection);
      }
    }
    if (DescriptionHintSectionLoc != null) {
      description.AddPrioritizedSection(Loc.T(DescriptionHintSectionLoc));
    }
    if (DescriptionExternalSections != null) {
      foreach (var externalSection in DescriptionExternalSections) {
        description.AddExternalSection(externalSection);
      }
    }
    _cachedDescription = description.Build();
    return _cachedDescription;
  }

  #endregion
}
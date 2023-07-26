// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using UnityEngine.UIElements;

namespace Automation.Utils {

/// <summary>Abstract tool that has description.</summary>
/// <remarks>
/// With no tweaking this class simply uses values from the TimberAPI specification. If more advanced logic is needed,
/// then the descendants can add more sections. Dynamic descriptions are also supported.
/// </remarks>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
public abstract class ToolWithDescription : CustomToolSystem.CustomTool {
  ToolDescription _cachedDescription;

  #region API
  /// <summary>
  /// The localization key of the text to present as the tool caption. If <c>null</c>,
  /// then <see cref="TimberApi.ToolSystem.ToolSpecification.NameLocKey"/> is used.
  /// </summary>
  protected string DescriptionTitleLoc = null;

  /// <summary>
  /// The localization key of the text to present as the tool description. If <c>null</c>,
  /// then <see cref="TimberApi.ToolSystem.ToolSpecification.DescriptionLocKey"/> is used.
  /// </summary>
  protected string DescriptionMainSectionLoc = null;

  /// <summary>The localization key of the optional text that is presented at the bottom of the main stuff.</summary>
  /// <remarks>If <c>null</c>, the it's not presented.</remarks>
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
  #endregion

  #region Tool overrides
  /// <inheritdoc/>
  public override ToolDescription Description() {
    if (_cachedDescription != null) {
      return _cachedDescription;
    }
    var description = new ToolDescription.Builder(Loc.T(DescriptionTitleLoc ?? ToolSpecification.NameLocKey));
    var descriptionText = new StringBuilder(Loc.T(DescriptionMainSectionLoc ?? ToolSpecification.DescriptionLocKey));
    if (DescriptionBullets != null) {
      foreach (var descriptionBullet in DescriptionBullets) {
        descriptionText.Append("\n" + SpecialStrings.RowStarter + Loc.T(descriptionBullet));
      }
    }
    // FIXME(IgorZ): Remove ColorizeText when Timberborn-Modding-Central#73 is fixed.
    description.AddSection(TextColors.ColorizeText(descriptionText.ToString()));
    if (DescriptionVisualSections != null) {
      foreach (var visualSection in DescriptionVisualSections) {
        description.AddSection(visualSection);
      }
    }
    if (DescriptionHintSectionLoc != null) {
      description.AddPrioritizedSection(TextColors.ColorizeText(Loc.T(DescriptionHintSectionLoc)));
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

}

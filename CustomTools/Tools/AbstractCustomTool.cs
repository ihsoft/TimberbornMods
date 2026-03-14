// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Text;
using Bindito.Core;
using IgorZ.CustomTools.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.CoreUI;
using Timberborn.Localization;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace IgorZ.CustomTools.Tools;

/// <summary>Base class for all custom tools.</summary>
public abstract class AbstractCustomTool : IDevModeTool, IToolDescriptor {

  #region API

  /// <summary>Returns text to show in the block object tool warning area.</summary>
  /// <remarks>
  /// This text can change and the changes will be reflected in UI immediately. Empty string or null value will hide the
  /// warning.
  /// </remarks>
  public virtual string GetWarningText() => null;

  /// <summary>The spec of the tool.</summary>
  /// <remarks>
  /// It can be used to extract more spec from the tools blueprint. E.g. <c>ToolSpec.GetSpec&lt;MyDataSpec&gt;()</c>.
  /// </remarks>
  public CustomToolSpec ToolSpec { get; private set; }

  /// <summary>Shortcut to <see cref="ILoc"/>.</summary>
  protected ILoc Loc { get; private set; }

  /// <summary>
  /// The localized text to present as the tool caption. If not overriden, then the string from the
  /// <see cref="AbstractCustomTool.ToolSpec"/> is used.
  /// </summary>
  /// <remarks>If <c>null</c> or empty, then the title will not be shown.</remarks>
  protected virtual string DescriptionTitle => Loc.T(ToolSpec.DisplayNameLocKey);

  /// <summary>Tells if the tool header should be shown in an entity element.</summary>
  /// <remarks>Entity header will have tool icon and text. By default, only text is shown in a compact view.</remarks>
  protected virtual bool NeedEntityHeader => false;

  /// <summary>The entity header element.</summary>
  /// <remarks>This element is created on initialization if <see cref="NeedEntityHeader"/> is specified.</remarks>
  protected VisualElement DescriptionHeaderElement { get; private set; }

  /// <summary>
  /// The localized text to present as the tool description. If not overriden, then the string from the
  /// <see cref="AbstractCustomTool.ToolSpec"/> is used.
  /// </summary>
  /// <remarks>If <c>null</c> or empty, then the description will not be shown.</remarks>
  protected virtual string DescriptionMainSection => Loc.T(ToolSpec.DescriptionLocKey);

  /// <summary>
  /// The localized option text that is presented at the bottom of the main stuff. It can be <c>null</c>.
  /// </summary>
  /// <remarks>This value should be set in the <see cref="Initialize"/> method and don't change after that.</remarks>
  protected string DescriptionHintSection = null;

  /// <summary>
  /// The visual elements to add to via <see cref="ToolDescription.Builder.AddExternalSection"/>. It can be <c>null</c>.
  /// </summary>
  /// <remarks>This value should be set in the <see cref="Initialize"/> method and don't change after that.</remarks>
  protected VisualElement[] DescriptionExternalSections = null;

  /// <summary>
  /// The visual elements to add to via <see cref="ToolDescription.Builder.AddSection(VisualElement)"/>.
  /// It can be <c>null</c>.
  /// </summary>
  /// <remarks>This value should be set in the <see cref="Initialize"/> method and don't change after that.</remarks>
  protected VisualElement[] DescriptionVisualSections = null;

  /// <summary>Extra localized strings to add to the description as "bullets". It can be <c>null</c>.</summary>
  /// <remarks>This value should be set in the <see cref="Initialize"/> method and don't change after that.</remarks>
  /// <seealso cref="SpecialStrings.RowStarter"/>
  protected string[] DescriptionBullets = null;

  /// <summary>Tells if any of the shift keys is held.</summary>
  protected bool IsShiftHeld => Keyboard.current.shiftKey.isPressed;

  /// <summary>Tells if any of the control keys is held.</summary>
  protected bool IsCtrlHeld => Keyboard.current.ctrlKey.isPressed;

  /// <summary>Tells if any of the alt keys is held.</summary>
  protected bool IsAltHeld => Keyboard.current.altKey.isPressed;

  /// <summary>Main initializer of the tool that sets its spec.</summary>
  /// <remarks>
  /// It is normally called by the BottomBar construction code. The mod code should only call it if the tool is internal
  /// and is not present anywhere on the bar. The method can only be called on a class, which doesn't have
  /// <see cref="ToolSpec"/> set.
  /// </remarks>
  /// <param name="toolSpec">The tool spec to assign to this tool class instance.</param>
  /// <exception cref="InvalidOperationException">if <see cref="ToolSpec"/> has already been set.</exception>
  public void InitializeTool(CustomToolSpec toolSpec) {
    if (ToolSpec != null) {
      throw new InvalidOperationException($"Tool is already initialized: {ToolSpec}");
    }
    ToolSpec = toolSpec;
    Initialize();
  }

  /// <summary>Initializes the tool. Do all logic here instead of the constructor.</summary>
  /// <remarks>
  /// In a usual case, the base initializers are called in the reversed order. I.e. the descendant does its
  /// initialization and, maybe, changes settings of the base class. Then, the base method is called to complete its
  /// part of initialization, based on the adjustments.
  /// </remarks>
  protected virtual void Initialize() {
    if (NeedEntityHeader) {
      var visualElementLoader = StaticBindings.DependencyContainer.GetInstance<VisualElementLoader>();
      DescriptionHeaderElement = visualElementLoader.LoadVisualElement("Game/EntityDescription/DescriptionHeader");
    }
  }

  #endregion

  #region IDevModeTool implementation

  /// <inheritdoc/>
  public abstract void Enter();

  /// <inheritdoc/>
  public abstract void Exit();

  /// <inheritdoc/>
  public bool IsDevMode => ToolSpec.DevMode;

  #endregion

  #region IToolDescriptor implementation

  /// <inheritdoc/>
  public virtual ToolDescription DescribeTool() {
    ToolDescription.Builder builder;
    if (DescriptionHeaderElement != null) {
      if (DescriptionTitle != null) {
        DescriptionHeaderElement.Q<Label>("Title").text = DescriptionTitle;
      }
      DescriptionHeaderElement.Q<Image>("Icon").sprite = ToolSpec.Icon.Asset;
      builder = new ToolDescription.Builder();
      builder.AddSection(DescriptionHeaderElement);
    } else {
      builder = !string.IsNullOrEmpty(DescriptionTitle)
          ? new ToolDescription.Builder(DescriptionTitle)
          : new ToolDescription.Builder();
    }
    var descriptionText = new StringBuilder();
    if (!string.IsNullOrEmpty(DescriptionMainSection)) {
      descriptionText.Append(DescriptionMainSection);
    }
    if (DescriptionBullets != null) {
      foreach (var descriptionBullet in DescriptionBullets) {
        descriptionText.Append("\n" + SpecialStrings.RowStarter + descriptionBullet);
      }
    }
    builder.AddSection(descriptionText.ToString());
    if (DescriptionVisualSections != null) {
      foreach (var visualSection in DescriptionVisualSections) {
        builder.AddSection(visualSection);
      }
    }
    if (DescriptionHintSection != null) {
      builder.AddPrioritizedSection(DescriptionHintSection);
    }
    if (DescriptionExternalSections != null) {
      foreach (var externalSection in DescriptionExternalSections) {
        builder.AddExternalSection(externalSection);
      }
    }
    return builder.Build();
  }

  #endregion

  #region Implementation

  /// <summary>Injects the dependencies. It has to be public to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc) {
    Loc = loc;
  }

  #endregion
}
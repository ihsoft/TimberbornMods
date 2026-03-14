// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using ConfigurableToolGroups.Services;
using ConfigurableToolGroups.UI;
using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

namespace IgorZ.CustomTools.Core;

abstract class AbstractLayoutElement(
    CustomToolsService customToolsService, ModdableToolGroupButtonFactory groupButtonFactory)
    : CustomBottomBarElement {

  const string RedClass = "bottom-bar-button--red";

  #region CustomBottomBarElement implementation

  /// <inheritdoc/>
  public override IEnumerable<BottomBarElement> GetElements() {
    var groupItems = customToolsService.CustomGroupSpecs
        .Where(x => x.ParentGroupId != null || x.Layout.ToLower() == Layout.ToLower())
        .Select(x => new ToolButtonOrGroup(x));
    var toolItems = customToolsService.CustomToolSpecs.Select(x => new ToolButtonOrGroup(x));
    var items = groupItems
        .Concat(toolItems)
        .OrderBy(x => x.Order)
        .ToArray();
    return items.Where(x => x.ParentGroupId == null)
        .Select(rootGroup => ToolGroupButtonWithItems(rootGroup.GroupSpec, null, items).ToBottomBarElement());
  }

  #endregion

  #region Implementation

  protected abstract string Layout { get; }


  readonly record struct ToolButtonOrGroup {
    public ToolButtonOrGroup(CustomToolGroupSpec groupSpec) { GroupSpec = groupSpec; }
    public ToolButtonOrGroup(CustomToolSpec toolSpec) { ToolSpec = toolSpec; }
    public readonly CustomToolGroupSpec GroupSpec;
    public readonly CustomToolSpec ToolSpec;
    public int Order => GroupSpec?.Order ?? ToolSpec.Order;
    public string ParentGroupId => GroupSpec?.ParentGroupId ?? ToolSpec?.GroupId;
  }

  ModdableToolGroupButton ToolGroupButtonWithItems(
      CustomToolGroupSpec customGroupSpec, ModdableToolGroupButton parent, ToolButtonOrGroup[] items) {
    var groupButton = CreateToolGroupButton(customGroupSpec, parent);
    var groupId = groupButton.Spec.Id;
    var childItems = items.Where(x => x.ParentGroupId == groupId).ToList();
    if (childItems.Count > 0) {
      DebugEx.Info(
          "Created custom tool group '{0}' in parent '{1}': children={2}", groupId, parent?.Spec.Id, childItems.Count);
    } else {
      DebugEx.Warning(
          "Created custom tool group '{0}' in parent '{1}', but it has no children!", groupId, parent?.Spec.Id);
    }
    foreach (var childItem in childItems) {
      if (childItem.ToolSpec != null) {
        var toolInstance = customToolsService.GetOrCreateCustomTool(childItem.ToolSpec);
        groupButton.AddChildTool(toolInstance, toolInstance.ToolSpec.Icon.Asset);
      } else if (childItem.GroupSpec != null) {
        groupButton.AddChildGroup(ToolGroupButtonWithItems(childItem.GroupSpec, groupButton, items));
      } else {
        throw new InvalidOperationException($"Unknown item type: {childItem}");
      }
    }
    return groupButton;
  }

  ModdableToolGroupButton CreateToolGroupButton(CustomToolGroupSpec customGroupSpec, ModdableToolGroupButton parent) {
    var toolGroupSpec = customGroupSpec.GetSpec<ToolGroupSpec>();
    if (toolGroupSpec == null) {
      throw new InvalidOperationException($"Missing ToolGroupSpec on custom group: {customGroupSpec}");
    }
    var groupId = toolGroupSpec.Id;
    ToolButtonColor? toolColor = customGroupSpec.Style.ToLower() switch {
        "blue" =>  ToolButtonColor.Blue,
        "green" => ToolButtonColor.Green,
        "red" => null,
        _ => throw new InvalidOperationException($"Unexpected tool group style: {customGroupSpec.Style}"),
    };
    var groupButton = groupButtonFactory.Create(toolGroupSpec, parent, toolColor ?? ToolButtonColor.Blue);
    // FIXME: One day, have it handled by the Moddable Groups.
    if (!toolColor.HasValue) {
      var buttonWrapper = groupButton.Root.Q<VisualElement>("ToolGroupButtonWrapper");
      if (buttonWrapper != null) {
        buttonWrapper.RemoveFromClassList(ToolGroupButtonFactory.BlueClass);
        buttonWrapper.AddToClassList(RedClass);
      } else {
        DebugEx.Warning("Cannot adjust style to RED on group button {0}", groupId);
      }
    }
    return groupButton;
  }

  #endregion
}

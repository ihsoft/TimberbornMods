// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bindito.Core;
using IgorZ.CustomTools.KeyBindings;
using IgorZ.CustomTools.Tools;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockObjectTools;
using Timberborn.BlueprintSystem;
using Timberborn.Common;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.CustomTools.Core;

/// <summary>Basic service for the custom tools' functionality.</summary>
public sealed class CustomToolsService(
    ISpecService specService, IContainer container, ToolService toolService, ToolGroupService toolGroupService,
    FeatureLimiterService featureLimiterService)
    : ILoadableSingleton, IPostLoadableSingleton {

  #region API

  /// <summary>All custom tool group specs.</summary>
  public ImmutableArray<CustomToolGroupSpec> CustomGroupSpecs { get; private set; }

  /// <summary>All custom tool specs.</summary>
  public ImmutableArray<CustomToolSpec> CustomToolSpecs { get; private set; }

  /// <summary>All custom tools instances by their IDs.</summary>
  public ImmutableDictionary<string, AbstractCustomTool> AllCustomTools => _allCustomTools.ToImmutableDictionary();
  readonly Dictionary<string, AbstractCustomTool> _allCustomTools = [];

  /// <summary>Mapping of blockobject tools to the blueprint name that they place.</summary>
  public ImmutableDictionary<string, BlockObjectTool> BlockObjectTools { get; private set; }

  /// <summary>Activates an arbitrary tool by its type.</summary>
  /// <remarks>
  /// The instance will be obtained via Bindito. If the type is no singleton, a new instance will be created and
  /// activated.
  /// </remarks>
  public void SelectToolByType(string toolTypeName) {
    var toolType = ReflectionsHelper.GetType(toolTypeName, typeof(ITool), needDefaultConstructor: false);
    SelectTool((ITool)container.GetInstance(toolType));
  }

  /// <summary>Activates the custom tool.</summary>
  public void SelectToolById(string customToolId) {
    if (!_allCustomTools.TryGetValue(customToolId, out var customTool)) {
      DebugEx.Warning("Custom tool ID '{0}' not found", customToolId);
      return;
    }
    SelectTool(customTool, customTool.ToolSpec.GroupId);
  }

  /// <summary>Activates the generic game tool.</summary>
  /// <remarks>
  /// The relevant group will be looked up via <see cref="ToolGroupService"/>. If group association is not found, then
  /// the tool will be activated without activating teh group.
  /// </remarks>
  public void SelectTool(ITool tool) {
    SelectTool(tool, toolGroupService._assignedToolGroups.GetOrDefault(tool)?.Id);
  }

  /// <summary>Activates the generic game tool.</summary>
  /// <remarks>
  /// The group ID will be used to activate the relevant group in the bottom bar. It's not required to be the group that
  /// holds the tool. If group ID is not provided, then no group activation is done.
  /// </remarks>
  public void SelectTool(ITool tool, string groupId) {
    if (tool is BlockObjectTool blockObjectTool) {
      DebugEx.Info("Activating BlockObjectTool tool: tool={0}, groupId={1}",
                   blockObjectTool.Template.Blueprint.Name, groupId);
    } else {
      DebugEx.Info("Activating tool: tool={0}, groupId={1}", tool, groupId);
    }
    if (groupId != null) {
      var toolGroupSpec = toolGroupService.GetGroup(groupId);
      toolGroupService.EnterToolGroup(toolGroupSpec);
    }
    toolService.SwitchTool(tool);
  }
      
  internal AbstractCustomTool GetOrCreateCustomTool(CustomToolSpec customToolSpec) {
    if (!_allCustomTools.TryGetValue(customToolSpec.Id, out var toolInstance)) {
      var toolType =
          ReflectionsHelper.GetType(customToolSpec.Type, typeof(AbstractCustomTool), needDefaultConstructor: false);
      toolInstance = (AbstractCustomTool)container.GetInstance(toolType);
      toolInstance.InitializeTool(customToolSpec);
      _allCustomTools[customToolSpec.Id] = toolInstance;
      DebugEx.Info("Created tool '{0}' in group '{1}'", toolType, customToolSpec.GroupId);
    }
    return toolInstance;
  }

  #endregion

  #region ILoadableSingleton implementation

  static readonly string[] AllowedLayouts = ["left", "middle", "right"];

  /// <inheritdoc/>
  public void Load() {
    var hasLoadErrors = false;

    // Load and verify the group specs.
    CustomGroupSpecs = specService.GetSpecs<CustomToolGroupSpec>().ToImmutableArray();
    DebugEx.Info("Loaded {0} custom tool group specs", CustomGroupSpecs.Length);
    var groupIds = new HashSet<string>();
    foreach (var groupSpec in CustomGroupSpecs) {
      if (groupSpec.ParentGroupId == null
          && (groupSpec.Layout == null || !AllowedLayouts.Contains(groupSpec.Layout.ToLower()))) {
        DebugEx.Error("Group spec has illegal layout: {0}", groupSpec);
        hasLoadErrors = true;
      }
      if (groupSpec.ParentGroupId != null && groupSpec.Layout != null) {
        DebugEx.Warning("Layout specification ignored in group spec: {0}", groupSpec);
      }
      var toolGroupSpec = groupSpec.GetSpec<ToolGroupSpec>();
      if (toolGroupSpec == null) {
        DebugEx.Error("Custom group blueprint has no ToolGroupSpec: {0}", groupSpec.Blueprint.Name);
        hasLoadErrors = true;
        continue;
      }
      groupIds.Add(toolGroupSpec.Id);
    }
    var unknownGroupIdSpecs = CustomGroupSpecs
        .Where(groupSpec => groupSpec.ParentGroupId != null && !groupIds.Contains(groupSpec.ParentGroupId));
    foreach (var groupIdRef in unknownGroupIdSpecs) {
      DebugEx.Error("Unknown group ID in group spec: {0}", groupIdRef);
      hasLoadErrors = true;
    }

    // Load and verify the tool specs.
    CustomToolSpecs = specService.GetSpecs<CustomToolSpec>()
        .Where(spec => featureLimiterService.IsAllowed(spec))
        .ToImmutableArray();
    HashSet<string> uniqueIds = [];
    DebugEx.Info("Loaded {0} custom tool specs", CustomGroupSpecs.Length);
    foreach (var toolSpec in CustomToolSpecs) {
      if (toolSpec.Id == null) {
        DebugEx.Error("Custom tool spec doesn't have ID: {0}", toolSpec);
        hasLoadErrors = true;
        continue;
      }
      if (!uniqueIds.Add(toolSpec.Id)) {
        DebugEx.Error("Custom tool spec specifies non-unique ID: {0}", toolSpec);
        hasLoadErrors = true;
        continue;
      }
      if (toolSpec.GroupId == null) {
        DebugEx.Error("Custom tool spec doesn't have group ID: {0}", toolSpec);
        hasLoadErrors = true;
        continue;
      }
      if (!groupIds.Contains(toolSpec.GroupId)) {
        DebugEx.Error("Custom tool spec specifies unknown group ID: {0}", toolSpec);
        hasLoadErrors = true;
        continue;
      }
      var toolType = ReflectionsHelper.GetType(
          toolSpec.Type, typeof(AbstractCustomTool), needDefaultConstructor: false, throwOnError: false);
      if (toolType == null) {
        DebugEx.Error("Custom tool spec requests unknown type: {0}", toolSpec);
        hasLoadErrors = true;
        continue;
      }
      var customToolBindingSpec = toolSpec.GetSpec<CustomToolBindingSpec>();
      if (customToolBindingSpec != null) {
        var bindingToolType = ReflectionsHelper.GetType(
            customToolBindingSpec.Type, typeof(AbstractCustomTool), needDefaultConstructor: false, throwOnError: false);
        if (bindingToolType == null) {
          DebugEx.Error("Custom tool binding spec requests unknown type: {0}", customToolBindingSpec);
          hasLoadErrors = true;
        }
      }
    }

    // Don't go further if there are critical errors.
    if (hasLoadErrors) {
      throw new InvalidOperationException("Some CustomTools specs cannot be loaded!");
    }
  }

  #endregion

  #region IPostLoadableSingleton implementation

  /// <inheritdoc/>
  public void PostLoad() {
    var blockObjectTools = new Dictionary<string, BlockObjectTool>();
    foreach (var tool in toolGroupService._assignedToolGroups.Keys) {
      if (tool is BlockObjectTool blockObjectTool) {
        var blueprintName = blockObjectTool.Template.Blueprint.Name;
        if (!blockObjectTools.TryAdd(blueprintName, blockObjectTool)) {
          DebugEx.Warning("Duplicate blueprint name: {0}, blockObjectTool={1}", blueprintName, blockObjectTool);
        }
      }
    }
    BlockObjectTools = blockObjectTools.ToImmutableDictionary();
  }

  #endregion
}

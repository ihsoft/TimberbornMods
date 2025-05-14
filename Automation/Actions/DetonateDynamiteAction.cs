// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using TimberApi.DependencyContainerSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BuilderPrioritySystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.Explosions;
using Timberborn.MapIndexSystem;
using Timberborn.Persistence;
using Timberborn.PrefabSystem;
using Timberborn.PrioritySystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.Actions;

/// <summary>Action that triggers the dynamite and then (optionally) places new one at the same spot.</summary>
/// <remarks>Use it to drill down deep holes in terrain.</remarks>
public sealed class DetonateDynamiteAction : AutomationActionBase {
  const string DescriptionLocKey = "IgorZ.Automation.DetonateDynamiteAction.Description";
  const string RepeatCountLocKey = "IgorZ.Automation.DetonateDynamiteAction.RepeatCountInfo";

  /// <summary>
  /// Number of times to place new dynamite. Any value less or equal to zero results in no extra actions on trigger.
  /// </summary>
  /// <remarks>
  /// A too big value is not a problem. When the bottom of the map is reached, the dynamite simply won't get placed.
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public int RepeatCount { get; private set; }

  /// <summary>Builder priority of the behavior owner object.</summary>
  /// <remarks>It must be captured before construction completes.</remarks>
  Priority _builderPriority;

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override string UiDescription {
    get {
      var res = Behavior.Loc.T(DescriptionLocKey);
      if (RepeatCount > 0) {
        res += Behavior.Loc.T(RepeatCountLocKey, RepeatCount);
      }
      return res;
    }
  }

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new DetonateDynamiteAction { TemplateFamily = TemplateFamily, RepeatCount = RepeatCount };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return behavior.GetComponentFast<Dynamite>();
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState || IsMarkedForCleanup) {
      return;
    }

    // The behavior object will get destroyed on detonate, so create an independent component.
    var component = new GameObject("#Automation_PlaceDynamiteAction").AddComponent<DetonateAndMaybeRepeatRule>();
    component.Setup(this, _builderPriority);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    var builderPrioritizable = Behavior.GetComponentFast<BuilderPrioritizable>();
    _builderPriority = builderPrioritizable.Priority;
    // The priority on a finished building is reset to "Normal", so track it while the object is in preview.
    builderPrioritizable.PriorityChanged += OnPriorityChanged;
  }

  /// <inheritdoc/>
  protected override void OnBehaviorToBeCleared() {
    base.OnBehaviorToBeCleared();
    var builderPrioritizable = Behavior.GetComponentFast<BuilderPrioritizable>();
    builderPrioritizable.PriorityChanged -= OnPriorityChanged;
  }

  /// <summary>Reacts on the builder's priority change while the object is in preview.</summary>
  void OnPriorityChanged(object sender, PriorityChangedEventArgs args) {
    _builderPriority = Behavior.GetComponentFast<BuilderPrioritizable>().Priority;
  }

  #endregion

  #region IGameSerializable implemenation

  static readonly PropertyKey<int> RepeatPropertyKey = new("Repeat");

  /// <summary>Loads action state and declaration.</summary>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    RepeatCount = objectLoader.Get(RepeatPropertyKey);
  }

  /// <summary>Saves action state and declaration.</summary>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(RepeatPropertyKey, RepeatCount);
  }

  #endregion

  #region MonoBehaviour object to repeat the action

  class DetonateAndMaybeRepeatRule : MonoBehaviour {
    const float MinDistanceToCheckOccupants = 2.0f;

    IBlockOccupancyService _blockOccupancyService;
    ToolButtonService _toolButtonService;
    ITerrainService _terrainService;
    BlockService _blockService;
    MapIndexService _mapIndexService;
    string _prefabName;

    BlockObject _blockObject;
    int _repeatCount;
    Priority _builderPriority;
    IAutomationCondition _condition; 

    /// <summary>Sets up the component and starts the actual monitoring of the object.</summary>
    public void Setup(DetonateDynamiteAction action, Priority builderPriority) {
      _blockObject = action.Behavior.BlockObject;
      _repeatCount = action.RepeatCount;
      _builderPriority = builderPriority;
      _condition = action.Condition.CloneDefinition();
      _prefabName = _blockObject.GetComponentFast<PrefabSpec>().Name;
      StartCoroutine(WaitAndPlace());
    }

    void Awake() {
      _blockOccupancyService = DependencyContainer.GetInstance<IBlockOccupancyService>();
      _toolButtonService = DependencyContainer.GetInstance<ToolButtonService>();
      _terrainService = DependencyContainer.GetInstance<ITerrainService>();
      _blockService = DependencyContainer.GetInstance<BlockService>();
      _mapIndexService = DependencyContainer.GetInstance<MapIndexService>();
    }

    IEnumerator WaitAndPlace() {
      var coordinates = _blockObject.Coordinates;
      var dynamite = _blockObject.GetComponentFast<Dynamite>();
      var effectiveDepth = 0;
      while (effectiveDepth < dynamite.Depth && coordinates.z > effectiveDepth) {
        var below = new Vector3Int(coordinates.x, coordinates.y, coordinates.z - effectiveDepth - 1);
        if (!_terrainService.UnsafeCellIsTerrain(_mapIndexService.CoordinatesToIndex3D(below))) {
          break;
        }
        effectiveDepth++;
      }
      yield return null;  // Act on the next frame to avoid synchronous complications.

      // Detonate the dynamite.
      yield return new WaitUntil(NoCharactersOnBlock);
      if (dynamite && dynamite.enabled) {
        HostedDebugLog.Fine(dynamite, "Detonate from automation!");
        dynamite.Trigger();
      }
      if (_repeatCount <= 0) {
        yield return YieldAbort();
      }
      
      // Wait for the old object to clean up.
      yield return new WaitUntil(() => !_blockObject);
      var expectedPlaceCoord = new Vector3Int(coordinates.x, coordinates.y, coordinates.z - effectiveDepth);
      var newHeight = _terrainService.GetTerrainHeightBelow(expectedPlaceCoord);
      if (newHeight == 0 || newHeight != expectedPlaceCoord.z) {
        DebugEx.Info("Reached the bottom of the floor at {0}", coordinates.XY());
        yield return YieldAbort();
      }

      // Place another dynamite of the same type and building priority.
      var dynamiteTool = _toolButtonService
          .ToolButtons.Select(x => x.Tool)
          .OfType<BlockObjectTool>()
          .First(x => x.Prefab.name.StartsWith(_prefabName));
      dynamiteTool.Place(new List<Placement> { new(expectedPlaceCoord) });
      if (!dynamiteTool._placedAnythingThisFrame) {
        DebugEx.Error("Cannot place new dynamite at {0}", expectedPlaceCoord);
        yield return YieldAbort();
      }
      BlockObject newDynamite;
      do {
        yield return null;
        newDynamite = _blockService.GetBottomObjectAt(expectedPlaceCoord);
      } while (!newDynamite);
      newDynamite.GetComponentFast<BuilderPrioritizable>().SetPriority(_builderPriority);
      newDynamite.GetComponentFast<AutomationBehavior>().AddRule(
          _condition,
          new DetonateDynamiteAction { RepeatCount = _repeatCount - 1 });
      HostedDebugLog.Fine(newDynamite, "Placed new item: priority={0}, tries={1}", _builderPriority, _repeatCount - 1);

      yield return YieldAbort();
    }

    IEnumerator YieldAbort() {
      Destroy(gameObject);
      yield break;
    }

    bool NoCharactersOnBlock() {
      if (!_blockObject || !_blockObject.enabled) {
        return true; // Terminate the check.
      }
      return !_blockOccupancyService.OccupantPresentOnArea(_blockObject, MinDistanceToCheckOccupants);
    }
  }

  #endregion
}
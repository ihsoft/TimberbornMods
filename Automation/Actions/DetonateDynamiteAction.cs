// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using TimberApi.DependencyContainerSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BuilderPrioritySystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.Explosions;
using Timberborn.Persistence;
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
  /// Number of times to place a new dynamite. Any value less or equal to zero results in no extra actions on trigger.
  /// </summary>
  /// <remarks>
  /// A too big value is not a problem. When the bottom of the map is reached, the dynamite simply won't get placed.
  /// </remarks>
  // ReSharper disable once MemberCanBePrivate.Global
  public int RepeatCount { get; private set; }

  /// <summary>Names of the tools to place various depths.</summary>
  /// <remarks>Action will only apply if the dynamite is of the known depths.</remarks>
  static readonly Dictionary<int, string> DynamiteDepthToToolNamePrefix = new() {
      {1 , "Dynamite."},
      {2 , "DoubleDynamite."},
      {3 , "TripleDynamite."},
  };

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
    var dynamite = behavior.GetComponentFast<Dynamite>();
    return dynamite && DynamiteDepthToToolNamePrefix.ContainsKey(dynamite.Depth);
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }

    // The behavior object will get destroyed on detonate, so create am independent component.
    var component = new GameObject("#Automation_PlaceDynamiteAction").AddComponent<DetonateAndMaybeRepeatRule>();
    component.Setup(Behavior.BlockObject, RepeatCount, _builderPriority);
  }

  /// <inheritdoc/>
  protected override void OnBehaviorAssigned() {
    base.OnBehaviorAssigned();
    var builderPrioritizable = Behavior.GetComponentFast<BuilderPrioritizable>();
    _builderPriority = builderPrioritizable.Priority;
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

    BlockObject _blockObject;
    int _repeatCount;
    Priority _builderPriority;

    /// <summary>Sets up the component and starts the actual monitoring of the object.</summary>
    public void Setup(BlockObject blockObject, int repeatCount, Priority builderPriority) {
      _blockObject = blockObject;
      _repeatCount = repeatCount;
      _builderPriority = builderPriority;
      StartCoroutine(WaitAndPlace());
    }

    void Awake() {
      _blockOccupancyService = DependencyContainer.GetInstance<IBlockOccupancyService>();
      _toolButtonService = DependencyContainer.GetInstance<ToolButtonService>();
      _terrainService = DependencyContainer.GetInstance<ITerrainService>();
      _blockService = DependencyContainer.GetInstance<BlockService>();
    }

    IEnumerator WaitAndPlace() {
      var coordinates = _blockObject.Coordinates;
      var dynamite = _blockObject.GetComponentFast<Dynamite>();
      var dynamiteDepth = dynamite.Depth;
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
      coordinates.z = _terrainService.CellHeight(coordinates.XY());
      if (coordinates.z <= 0) {
        DebugEx.Fine("Reached the bottom of the map at {0}", coordinates.XY());
        yield return YieldAbort();
      }

      // Place another dynamite of the same type and building priority.
      var toolNamePrefix = DynamiteDepthToToolNamePrefix[dynamiteDepth];
      var dynamiteTool = _toolButtonService
          .ToolButtons.Select(x => x.Tool)
          .OfType<BlockObjectTool>()
          .First(x => x.Prefab.name.StartsWith(toolNamePrefix));
      dynamiteTool.Place(new List<Placement> { new(coordinates) });
      BlockObject newDynamite;
      do {
        yield return null;
        newDynamite = _blockService.GetBottomObjectAt(coordinates);
      } while (newDynamite == null);
      newDynamite.GetComponentFast<BuilderPrioritizable>().SetPriority(_builderPriority);
      newDynamite.GetComponentFast<AutomationBehavior>().AddRule(
        new ObjectFinishedCondition(),
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
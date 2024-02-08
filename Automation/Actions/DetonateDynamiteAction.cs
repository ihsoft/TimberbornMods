// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Automation.Conditions;
using Automation.Core;
using TimberApi.DependencyContainerSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.Explosions;
using Timberborn.Persistence;
using Timberborn.ToolSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace Automation.Actions {

/// <summary>Action that triggers the dynamite and then (optionally) places new one at the same spot.</summary>
/// <remarks>Use it to drill down deep holes in terrain.</remarks>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class DetonateDynamiteAction : AutomationActionBase {
  const string DescriptionLocKey = "IgorZ.Automation.DetonateDynamiteAction.Description";
  const string RepeatCountLocKey = "IgorZ.Automation.DetonateDynamiteAction.RepeatCountInfo";

  /// <summary>
  /// Number of times to place a new dynamite. Any value less or equal to zero results in no extra actions on trigger.
  /// </summary>
  /// <remarks>
  /// A too big value is not a problem. When the bottom of the map is reached, the dynamite simply won't get placed.
  /// </remarks>
  public int RepeatCount { get; private set; }

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
    return behavior.GetComponentFast<Dynamite>() != null;
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    // This object will get destroyed on detonate, so create am independent component.
    var component = new GameObject("#Automation_PlaceDynamiteAction").AddComponent<DetonateAndRepeatRule>();
    component.blockObject = Behavior.BlockObject;
    component.repeatCount = RepeatCount;
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

  #region MonoBehavior object to handle action repeat
  class DetonateAndRepeatRule : MonoBehaviour {
    const float MinDistanceToCheckOccupants = 2.0f;

    IBlockOccupancyService _blockOccupancyService;

    public BlockObject blockObject;
    public int repeatCount;

    void Awake() {
      _blockOccupancyService = DependencyContainer.GetInstance<IBlockOccupancyService>();
      StartCoroutine(WaitAndPlace());
    }

    IEnumerator WaitAndPlace() {
      yield return null; // Act on the next frame to avoid synchronous complications.

      yield return new WaitUntil(NoCharactersOnBlock);
      if (blockObject != null && blockObject.enabled) {
        var dynamite = blockObject.GetComponentFast<Dynamite>();
        if (dynamite == null) {
          DebugEx.Warning("Dynamite prefab not found on block object");
          yield break;
        }
        DebugEx.Fine("Detonate dynamite: coordinates={0}, tries={1}", blockObject.Coordinates, repeatCount);
        dynamite.Trigger();
      }
      if (repeatCount <= 0) {
        yield break;
      }
      if (blockObject.Coordinates.z <= 1) {
        DebugEx.Fine("Reached the bottom of the map. Abort placing dynamite.");
        yield break;
      }
      
      var dynamiteTool = DependencyContainer.GetInstance<ToolButtonService>()
          .ToolButtons.Select(x => x.Tool)
          .OfType<BlockObjectTool>()
          .FirstOrDefault(x => x.Prefab.name.StartsWith("Dynamite"));
      if (dynamiteTool == null) {
        DebugEx.Error("Cannot execute dynamite place tool");
        Destroy(gameObject);
        yield break;
      }

      // Wait for the old object to clean up and place another one.
      var coordinates = blockObject.Coordinates;
      yield return new WaitUntil(() => blockObject == null);
      coordinates.z = coordinates.z - 1;
      dynamiteTool.Place(new List<Placement> { new(coordinates) });
      var blockService = DependencyContainer.GetInstance<BlockService>();
      BlockObject newDynamite;
      do {
        yield return null;
        newDynamite = blockService.GetBottomObjectAt(coordinates);
      } while (newDynamite == null);

      newDynamite.GetComponentFast<AutomationBehavior>().AddRule(
          new ObjectFinishedCondition(),
          new DetonateDynamiteAction { RepeatCount = repeatCount - 1 });
      Destroy(gameObject);
    }

    bool NoCharactersOnBlock() {
      if (blockObject == null || !blockObject.enabled) {
        return true; // Terminate the check.
      }
      return !_blockOccupancyService.OccupantPresentOnArea(blockObject, MinDistanceToCheckOccupants);
    }
  }
  #endregion
}

}

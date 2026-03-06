// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;
using Timberborn.BuilderPrioritySystem;
using Timberborn.Common;
using Timberborn.Coordinates;
using Timberborn.Explosions;
using Timberborn.MapIndexSystem;
using Timberborn.PrioritySystem;
using Timberborn.TerrainSystem;
using Timberborn.ToolButtonSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class DynamiteScriptableComponent : ScriptableComponentBase {

  const string DetonateActionLocKey = "IgorZ.Automation.Scriptable.Dynamite.Action.Detonate";
  const string DetonateAndRepeatActionLocKey = "IgorZ.Automation.Scriptable.Dynamite.Action.DetonateAndRepeat";

  const string DetonateActionName = "Dynamite.Detonate";
  const string DetonateAndRepeatActionName = "Dynamite.DetonateAndRepeat";

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Dynamite";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return behavior.GetComponent<Dynamite>() ? [DetonateActionName, DetonateAndRepeatActionName] : [];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var dynamite = behavior.GetComponent<Dynamite>();
    if (!dynamite) {
      throw new ScriptError.BadStateError(behavior, "Dynamite component not found");
    }
    return name switch {
        DetonateActionName => _ => DetonateAction(behavior),
        DetonateAndRepeatActionName => args => DetonateAndRepeatAction(behavior, args),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior _) {
    return name switch {
        DetonateActionName => DetonateActionDef,
        DetonateAndRepeatActionName => DetonateAndRepeatActionDef,
        _ => throw new UnknownActionException(name),
    };
  }


  /// <inheritdoc/>
  public override void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is  DetonateActionName or DetonateAndRepeatActionName) {
      behavior.GetOrCreate<DynamiteStateController>().AddAction(actionOperator);
    }
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is  DetonateActionName or DetonateAndRepeatActionName) {
      behavior.GetOrThrow<DynamiteStateController>().RemoveAction(actionOperator);
    }
  }

  #endregion

  #region Actions

  ActionDef DetonateActionDef => _detonateActionDef ??= new ActionDef {
      ScriptName = DetonateActionName,
      DisplayName = Loc.T(DetonateActionLocKey),
      Arguments = [],
  };
  ActionDef _detonateActionDef;

  ActionDef DetonateAndRepeatActionDef => _detonateAndRepeatActionDef ??= new ActionDef {
      ScriptName = DetonateAndRepeatActionName,
      DisplayName = Loc.T(DetonateAndRepeatActionLocKey),
      Arguments = [
          new ValueDef {
              ValueType = ScriptValue.TypeEnum.Number,
              ValueFormatter = x => x.AsInt.ToString(),
              RuntimeValueValidator = ValueDef.RangeCheckValidatorInt(min: 1, max: 6),
          },
      ],
  };
  ActionDef _detonateAndRepeatActionDef;

  static void DetonateAction(AutomationBehavior behavior) {
    behavior.GetOrThrow<DynamiteStateController>().DetonateAndRepeat(behavior, 0);
  }

  static void DetonateAndRepeatAction(AutomationBehavior behavior, ScriptValue[] args) {
    AssertActionArgsCount(DetonateAndRepeatActionName, args, 1);
    behavior.GetOrThrow<DynamiteStateController>().DetonateAndRepeat(behavior, args[0].AsInt);
  }

  #endregion

  #region Dynamite state controller

  /// <summary>
  /// Creates a custom status icon that indicates that the storage is being emptying. If the status is changed
  /// externally, then hides the status and notifies the action.
  /// </summary>
  internal sealed class DynamiteStateController : AbstractStatusTracker, IAwakableComponent {

    public void DetonateAndRepeat(AutomationBehavior behavior, int repeatCount) {
      // The behavior object will get destroyed on detonate, so create an independent component.
      var component = new GameObject("#Automation_PlaceDynamiteAction").AddComponent<DetonateAndMaybeRepeatRule>();
      component.StartCoroutine(component.WaitAndPlace(behavior, _builderPriority, repeatCount));
    }

    BuilderPrioritizable _builderPrioritizable;
    Priority _builderPriority;

    public void Awake() {
      // The priority on a finished building is reset to "Normal", so track it while the object is in preview.
      _builderPrioritizable = AutomationBehavior.GetComponent<BuilderPrioritizable>();
      if (_builderPrioritizable) {
        _builderPriority = _builderPrioritizable.Priority;
        _builderPrioritizable.PriorityChanged += OnPriorityChanged;
      }
    }

    void OnPriorityChanged(object sender, PriorityChangedEventArgs args) {
      _builderPriority = _builderPrioritizable.Priority;
    }
  }

  #endregion

  #region MonoBehaviour object to repeat the action

  class DetonateAndMaybeRepeatRule : MonoBehaviour {
    const float MinDistanceToCheckOccupants = 2.0f;
    //FIXME: lookup this value from the rules.
    const string TemplateFamily = "Dynamite.Digging";

    /// <summary>Sets up the component and starts the actual monitoring of the object.</summary>
    public IEnumerator WaitAndPlace(AutomationBehavior behavior, Priority builderPriority, int repeatCount) {
      var blueprintName = behavior.GetComponent<BlockObjectSpec>().Blueprint.Name;
      var dynamite = behavior.GetComponent<Dynamite>();
      var coordinates = behavior.BlockObject.Coordinates;
      var terrainService = StaticBindings.DependencyContainer.GetInstance<ITerrainService>();

      // Find the new depth under the dynamite when it explodes. It can be placed on an overhang!
      var effectiveDepth = 0;
      var mapIndexService = StaticBindings.DependencyContainer.GetInstance<MapIndexService>();
      while (effectiveDepth < dynamite.Depth && coordinates.z > effectiveDepth) {
        var below = new Vector3Int(coordinates.x, coordinates.y, coordinates.z - effectiveDepth - 1);
        if (!terrainService.UnsafeCellIsTerrain(mapIndexService.CoordinatesToIndex3D(below))) {
          break;
        }
        effectiveDepth++;
      }
      yield return null;  // Act on the next frame to avoid synchronous complications.

      // Detonate the dynamite, but wait till all beavers are off the block.
      var blockOccupancyService = StaticBindings.DependencyContainer.GetInstance<IBlockOccupancyService>();
      while (dynamite
             && blockOccupancyService.OccupantPresentOnArea(behavior.BlockObject, MinDistanceToCheckOccupants)) {
        yield return new WaitForFixedUpdate();
      }
      if (dynamite) {
        HostedDebugLog.Fine(dynamite, "Detonate from automation!");
        dynamite.Trigger();
      }
      if (repeatCount <= 0) {
        yield return YieldAbort();
      }

      // Wait for the old object to clean up.
      // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
      while (dynamite) {
        yield return new WaitForFixedUpdate();
      }
      var expectedPlaceCoord = new Vector3Int(coordinates.x, coordinates.y, coordinates.z - effectiveDepth);
      var newHeight = terrainService.GetTerrainHeightBelow(expectedPlaceCoord);
      if (newHeight == 0 || newHeight != expectedPlaceCoord.z) {
        DebugEx.Info("Reached the bottom of the floor at {0}", coordinates.XY());
        yield return YieldAbort();
      }

      // Place another dynamite of the same type and building priority.
      var toolButtonService = StaticBindings.DependencyContainer.GetInstance<ToolButtonService>();
      var dynamiteTool = toolButtonService
          .ToolButtons.Select(x => x.Tool)
          .OfType<BlockObjectTool>()
          .First(x => x.Template.Blueprint.Name == blueprintName);
      dynamiteTool.Place(new List<Placement> { new(expectedPlaceCoord) });
      if (!dynamiteTool._placedAnythingThisFrame) {
        DebugEx.Error("Cannot place new dynamite at {0}", expectedPlaceCoord);
        yield return YieldAbort();
      }
      BlockObject newDynamite;
      var blockService = StaticBindings.DependencyContainer.GetInstance<BlockService>();
      do {
        yield return null;
        newDynamite = blockService.GetBottomObjectAt(expectedPlaceCoord);
      } while (!newDynamite);

      var prioritizable = newDynamite.GetComponent<BuilderPrioritizable>();
      if (prioritizable) {
        prioritizable.SetPriority(builderPriority);
      }

      var newCondition = new ScriptedCondition();
      newCondition.SetExpression("(eq (sig Constructable.OnUnfinished.State) 'finished')");
      var newAction = new ScriptedAction {
          TemplateFamily = TemplateFamily,
      };
      newAction.SetExpression(
          repeatCount > 1
              ? $"(act Dynamite.DetonateAndRepeat {(repeatCount - 1) * 100})"
              : $"(act Dynamite.Detonate)");
      newDynamite.GetComponent<AutomationBehavior>().AddRule(newCondition, newAction);
      HostedDebugLog.Fine(
          newDynamite, "Placed new dynamite: priority={0}, tries={1}", builderPriority, repeatCount - 1);

      yield return YieldAbort();
    }

    IEnumerator YieldAbort() {
      Destroy(gameObject);
      yield break;
    }
  }

  #endregion
}

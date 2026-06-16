// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
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
    GetComponentOrThrow<Dynamite>(behavior);  // Verify only.
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
    if (actionOperator.ActionName is DetonateActionName or DetonateAndRepeatActionName) {
      behavior.GetOrCreate<DynamiteStateController>().AddAction(actionOperator);
    }
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is DetonateActionName or DetonateAndRepeatActionName) {
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
              DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
              DisplayNumericFormatRange = (1, 6),
              ArgumentValidator = ValidateRepeatCount,
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
      // The behavior object will get destroyed on detonating, so create an independent component.
      var component = new GameObject("#Automation_PlaceDynamiteAction").AddComponent<DetonateAndMaybeRepeatRule>();
      // Game calls are wrapped in a service to make the coroutine orchestration testable without simulating the map.
      component.RepeatService = _repeatService
          ?? throw new InvalidOperationException("Dynamite repeat service is not initialized");
      component.StartCoroutine(component.WaitAndPlace(behavior, _builderPriority, repeatCount));
    }

    DynamiteRepeatService _repeatService;
    BuilderPrioritizable _builderPrioritizable;
    Priority _builderPriority;

    [Inject]
    public void InjectDependencies(DynamiteRepeatService repeatService) {
      _repeatService = repeatService;
    }

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

  internal class DetonateAndMaybeRepeatRule : MonoBehaviour {
    public DynamiteRepeatService RepeatService { get; set; }

    /// <summary>Sets up the component and starts the actual monitoring of the object.</summary>
    public IEnumerator WaitAndPlace(AutomationBehavior behavior, Priority builderPriority, int repeatCount) {
      var repeatService = RepeatService
          ?? throw new InvalidOperationException("Dynamite repeat service is not initialized");
      var target = repeatService.CaptureTarget(behavior);
      var effectiveDepth = repeatService.GetEffectiveDepth(target);
      yield return null;  // Act on the next frame to avoid synchronous complications.

      // Detonate the dynamite, but wait till all beavers are off the block.
      while (repeatService.IsDynamiteAlive(target) && repeatService.IsOccupantPresent(target)) {
        yield return new WaitForFixedUpdate();
      }
      if (repeatService.IsDynamiteAlive(target)) {
        repeatService.Detonate(target);
      }
      if (repeatCount <= 0) {
        yield return YieldAbort();
      }

      // Wait for the old object to clean up.
      // ReSharper disable once LoopVariableIsNeverChangedInsideLoop
      while (repeatService.IsDynamiteAlive(target)) {
        yield return new WaitForFixedUpdate();
      }
      var expectedPlaceCoord = repeatService.GetExpectedPlaceCoordinates(target, effectiveDepth);
      if (!repeatService.CanPlaceAt(expectedPlaceCoord)) {
        yield return YieldAbort();
      }

      // Place another dynamite of the same type and building priority.
      if (!repeatService.TryPlaceDynamite(target.BlueprintName, expectedPlaceCoord)) {
        yield return YieldAbort();
      }
      BlockObject newDynamite;
      do {
        yield return null;
        newDynamite = repeatService.GetPlacedDynamite(expectedPlaceCoord);
      } while (!newDynamite);

      repeatService.ConfigurePlacedDynamite(newDynamite, builderPriority, repeatCount);
      HostedDebugLog.Fine(
          newDynamite, "Placed new dynamite: priority={0}, tries={1}", builderPriority, repeatCount - 1);

      yield return YieldAbort();
    }

    IEnumerator YieldAbort() {
      Destroy(gameObject);
      yield break;
    }
  }

  /// <summary>
  /// Wraps game-specific calls used by <see cref="DetonateAndMaybeRepeatRule"/>. The coroutine stays responsible for
  /// the operation order, while this service isolates terrain, occupancy, tools, and block lookup for testing.
  /// </summary>
  internal class DynamiteRepeatService {
    const float MinDistanceToCheckOccupants = 2.0f;
    //FIXME: lookup this value from the rules.
    const string TemplateFamily = "Dynamite.Digging";

    public virtual DynamiteRepeatTarget CaptureTarget(AutomationBehavior behavior) {
      return new DynamiteRepeatTarget(
          behavior.GetComponent<BlockObjectSpec>().Blueprint.Name,
          behavior.GetComponentOrFail<Dynamite>(),
          behavior.BlockObject,
          behavior.BlockObject.Coordinates);
    }

    public virtual int GetEffectiveDepth(DynamiteRepeatTarget target) {
      var terrainService = StaticBindings.DependencyContainer.GetInstance<ITerrainService>();
      var mapIndexService = StaticBindings.DependencyContainer.GetInstance<MapIndexService>();
      var effectiveDepth = 0;
      while (effectiveDepth < target.Dynamite.Depth && target.Coordinates.z > effectiveDepth) {
        var below = new Vector3Int(
            target.Coordinates.x, target.Coordinates.y, target.Coordinates.z - effectiveDepth - 1);
        if (!terrainService.UnsafeCellIsTerrain(mapIndexService.CoordinatesToIndex3D(below))) {
          break;
        }
        effectiveDepth++;
      }
      return effectiveDepth;
    }

    public virtual bool IsOccupantPresent(DynamiteRepeatTarget target) {
      var blockOccupancyService = StaticBindings.DependencyContainer.GetInstance<IBlockOccupancyService>();
      return blockOccupancyService.OccupantPresentOnArea(target.BlockObject, MinDistanceToCheckOccupants);
    }

    public virtual bool IsDynamiteAlive(DynamiteRepeatTarget target) {
      return target.Dynamite;
    }

    public virtual void Detonate(DynamiteRepeatTarget target) {
      HostedDebugLog.Fine(target.Dynamite, "Detonate from automation!");
      target.Dynamite.Trigger();
    }

    public virtual Vector3Int GetExpectedPlaceCoordinates(DynamiteRepeatTarget target, int effectiveDepth) {
      return new Vector3Int(target.Coordinates.x, target.Coordinates.y, target.Coordinates.z - effectiveDepth);
    }

    public virtual bool CanPlaceAt(Vector3Int expectedPlaceCoord) {
      var terrainService = StaticBindings.DependencyContainer.GetInstance<ITerrainService>();
      var newHeight = terrainService.GetTerrainHeightBelow(expectedPlaceCoord);
      if (newHeight != 0 && newHeight == expectedPlaceCoord.z) {
        return true;
      }
      DebugEx.Info("Reached the bottom of the floor at {0}", expectedPlaceCoord.XY());
      return false;
    }

    public virtual bool TryPlaceDynamite(string blueprintName, Vector3Int expectedPlaceCoord) {
      var toolButtonService = StaticBindings.DependencyContainer.GetInstance<ToolButtonService>();
      var dynamiteTool = toolButtonService
          .ToolButtons.Select(x => x.Tool)
          .OfType<BlockObjectTool>()
          .First(x => x.Template.Blueprint.Name == blueprintName);
      dynamiteTool.Place(new List<Placement> { new(expectedPlaceCoord) });
      if (dynamiteTool._placedAnythingThisFrame) {
        return true;
      }
      DebugEx.Error("Cannot place new dynamite at {0}", expectedPlaceCoord);
      return false;
    }

    public virtual BlockObject GetPlacedDynamite(Vector3Int expectedPlaceCoord) {
      var blockService = StaticBindings.DependencyContainer.GetInstance<BlockService>();
      return blockService.GetBottomObjectAt(expectedPlaceCoord);
    }

    public virtual void ConfigurePlacedDynamite(BlockObject newDynamite, Priority builderPriority, int repeatCount) {
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
    }
  }

  /// <summary>Snapshot of the original dynamite state captured before the object can be destroyed.</summary>
  internal readonly record struct DynamiteRepeatTarget(
      string BlueprintName, Dynamite Dynamite, BlockObject BlockObject, Vector3Int Coordinates);

  static void ValidateRepeatCount(IValueExpr expr) {
    if (!expr.IsConstantValue() || expr.ValueType != ScriptValue.TypeEnum.Number) {
      throw new ScriptError.ParsingError($"Repeat count must be a constant value");
    }
  }

  #endregion
}

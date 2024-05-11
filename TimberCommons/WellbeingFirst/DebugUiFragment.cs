// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Linq;
using System.Text;
using IgorZ.TimberDev.UI;
using TimberApi.DependencyContainerSystem;
using TimberApi.UiBuilderSystem;
using Timberborn.Attractions;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.NeedSystem;
using Timberborn.RecoveredGoodSystem;
using Timberborn.WalkingSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;
using UnityEngine.UIElements;
namespace IgorZ.TimberCommons.WellbeingFirst {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class DebugUiFragment : IEntityPanelFragment {
  readonly UIBuilder _builder;
  
  VisualElement _root;
  Label _infoLabel;
  Button _resetBehavior;
  Citizen _citizen;
  BehaviorManager _behaviorManager;
  
  public DebugUiFragment(UIBuilder builder) {
    _builder = builder;
  }

  public VisualElement InitializeFragment() {
    _infoLabel = _builder.Presets().Labels().Label(color: UiFactory.PanelNormalColor);
    _resetBehavior = _builder.Presets().Buttons().Button(text: "Cancel current work");
    _resetBehavior.clicked += () => {
      TryAbortingCurrentBehavior();
    };

    _root = _builder.CreateFragmentBuilder()
        .AddComponent(_infoLabel)
        .AddComponent(_resetBehavior)
        .BuildAndInitialize();
    _root.ToggleDisplayStyle(visible: false);
    return _root;
  }

  void TryAbortingCurrentBehavior() {
    var behavior = _behaviorManager._runningBehavior;
    switch (behavior) {
      case CarryRootBehavior carryBehavior:
        CancelCarryRootBehavior(carryBehavior);
        break;
      case AttractionNeedBehavior attractionBehavior:
        CancelAttractionBehavior(attractionBehavior, _behaviorManager._behaviorAgent);
        break;
    }
  }

  bool CheckCanCancel() {
    return _behaviorManager._runningBehavior is CarryRootBehavior or AttractionNeedBehavior;
  }

  void CancelCarryRootBehavior(CarryRootBehavior behavior) {
    if (behavior._goodCarrier.IsCarrying) {
      //var coord = CoordinateSystem.WorldToGridInt(_citizen.TransformFast.position);
      var coord = FixedWorldToGridInt(_citizen.TransformFast.position);
      DebugEx.Warning("*** dropping: name={0}, amount={1}, pos={2}, coords={3}",
                      behavior._goodCarrier.CarriedGoods.GoodId, behavior._goodCarrier.CarriedGoods.Amount,
                      _citizen.TransformFast.position, coord);
      var stackSpawner = DependencyContainer.GetInstance<RecoveredGoodStackSpawner>();
      stackSpawner.AddAwaitingGoods(coord, new []{ behavior._goodCarrier.CarriedGoods});
      behavior._goodCarrier.EmptyHands();
    }
    if (behavior._goodReserver.HasReservedStock) {
      var good = behavior._goodReserver.StockReservation.GoodAmount;
      DebugEx.Warning("*** unreserve stock: name={0}, amount={1}, at={2}",
                      good.GoodId, good.Amount, behavior._goodReserver.StockReservation.Inventory);
      behavior._goodReserver.UnreserveStock();
    }
    if (behavior._goodReserver.HasReservedCapacity) {
      var good = behavior._goodReserver.CapacityReservation.GoodAmount;
      DebugEx.Warning("*** unreserve capacity: name={0}, amount={1}, at={2}",
                      good.GoodId, good.Amount, behavior._goodReserver.CapacityReservation.Inventory);
      behavior._goodReserver.UnreserveCapacity();
    }
    AbortCurrentBehavior(behavior._walkToAccessibleExecutor._walker);
  }

  static Vector3Int FixedWorldToGridInt(Vector3 v) {
    return new Vector3(v.x + 0.1f, v.z + 0.1f, v.y + 0.1f).FloorToInt();
  }

  void AbortCurrentBehavior(Walker walker) {
    var behavior = _behaviorManager._runningBehavior;
    var executor = _behaviorManager._runningExecutor;
    var behaviorName = behavior ? behavior.ComponentName + DebugEx.ObjectToString(behavior) : "NULL";
    var executorName = executor != null ? executor.GetName() + DebugEx.ObjectToString(executor) : "NULL";
    DebugEx.Warning("*** Aborting: behavior={0}, executor={1}", behaviorName, executorName);
    if (walker) {
      walker.Stop();
    }
    _behaviorManager._runningBehavior = null;
    _behaviorManager._runningExecutor = null;
    
    //FIXME
    // foreach (var beh in _behaviorManager._rootBehaviors) {
    //   DebugEx.Warning("*** root behavior: {0}{1}", beh.ComponentName, DebugEx.ObjectToString(beh));
    // }
  }

  void CancelAttractionBehavior(AttractionNeedBehavior behavior, BehaviorAgent agent) {
    var executor = agent.GetComponentFast<WalkInsideExecutor>();
    executor._enterer.UnreserveSlot();
    AbortCurrentBehavior(executor._walker);
  }

  public void ShowFragment(BaseComponent entity) {
    _citizen = entity.GetComponentFast<Citizen>();
    _behaviorManager = entity.GetComponentFast<BehaviorManager>();
    _root.ToggleDisplayStyle(visible: _citizen);
    // if (_citizen) {
    //   PrintAllComponents(_citizen);
    // }
  }

  static void PrintAllComponents(BaseComponent component) {
    var lines = new StringBuilder();
    lines.AppendLine(new string('*', 10));
    lines.AppendLine($"Components on {DebugEx.BaseComponentToString(component)}:");
    var names = component.AllComponents.Select(x => x.GetType().ToString()).OrderBy(x => x);
    lines.AppendLine(string.Join("\n", names));
    lines.AppendLine(new string('*', 10));
    
    DebugEx.Warning(lines.ToString());
  }

  public void ClearFragment() {
    _citizen = null;
    _behaviorManager = null;
    _root.ToggleDisplayStyle(visible: false);
  }

  public void UpdateFragment() {
    if (!_citizen) {
      return;
    }
    var manager = _citizen.GetComponentFast<NeedManager>();
    var info = "Needs:";
    foreach (var need in manager._needs.AllNeeds) {
      if (!need.IsInCriticalState) {
        continue;
      }
      info += string.Format("\nneed={0}, isCritical={1}", need.Specification.Id, need.IsInCriticalState);
    }
    var behavior = _behaviorManager._runningBehavior;
    if (behavior) {
      info += "\nBehavior: " + behavior.ComponentName + DebugEx.ObjectToString(behavior);
    }
    _infoLabel.text = info;
    _resetBehavior.ToggleDisplayStyle(visible: CheckCanCancel());
  }
}

}

// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using TimberApi.DependencyContainerSystem;
using Timberborn.Attractions;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.Characters;
using Timberborn.Common;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSystem;
using Timberborn.RecoveredGoodSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.WellbeingFirst {

sealed class HaulerWellbeingOptimizer : BaseComponent {
  
  public Character Character { get; private set; }
  public NeedManager NeedManager { get; private set; }
  public BehaviorManager BehaviorManager { get; private set; }
  public ActionDurationCalculator DurationCalculator { get; private set; }
  public Walker Walker { get; private set; }

  public HashSet<string> CriticalNeedsForRole {
    get {
      if (IsHauler()) {
        return HaulerCriticalNeeds;
      }
      if (IsBuilder()) {
        return BuilderCriticalNeeds;
      }
      return null;
    }
  }

  static readonly HashSet<string> HaulerCriticalNeeds = new() { "Thirst" };
  static readonly HashSet<string> BuilderCriticalNeeds = new() { "Thirst", "Hunger" };

  public IEnumerable<string> NeedsInCriticalState =>
      NeedManager._needs.AllNeeds.Where(x => x.IsInCriticalState).Select(x => x.Specification.Id);

  public bool NeedsOptimization => !DisableOptimization && (IsBuilder() || IsHauler());

  internal bool DisableOptimization;

  Worker _worker;
  const string HaulWorkplaceBehaviorName = "HaulWorkplaceBehavior";
  const string BuilderHubWorkplaceBehaviorName = "BuilderHubWorkplaceBehavior";
  const float EmptyHandsTravelTimeThreshold = 2;
  const float CarryingGoodTravelTimeThreshold = 6;

  void Awake() {
    Character = GetComponentFast<Character>();
    NeedManager = GetComponentFast<NeedManager>();
    NeedManager.NeedChangedIsFavorable += OnNeedChangedIsFavorable; 
    BehaviorManager = GetComponentFast<BehaviorManager>();
    DurationCalculator = GetComponentFast<ActionDurationCalculator>();
    Walker = GetComponentFast<Walker>();
    _worker = GetComponentFast<Worker>();
  }

  void OnNeedChangedIsFavorable(object sender, NeedChangedIsFavorableEventArgs arg) {
    if (!CheckCanCancelCurrentBehavior()) {
      return;
    }
    HashSet<string> criticalNeeds = null;
    if (IsHauler()) {
      criticalNeeds = HaulerCriticalNeeds;
    } else if (IsBuilder()) {
      criticalNeeds = BuilderCriticalNeeds;
    } else {
      return;  // Don't affect behavior of all other workers.
    }
    if (!criticalNeeds.Contains(arg.NeedSpecification.Id)) {
      return;  // Not an affecting need.
    }
    var behavior = BehaviorManager._runningBehavior as CarryRootBehavior;
    if (!behavior) {
      return;
    }
    var timeThreshold = EmptyHandsTravelTimeThreshold;
    var timeToDestination = TimeToDestination();
    if (behavior._goodCarrier.IsCarrying) {
      if (timeToDestination > CarryingGoodTravelTimeThreshold) {
        timeThreshold = CarryingGoodTravelTimeThreshold;
      } else {
        return;
      }
    }
    if (timeToDestination < timeThreshold) {
      return;  // Don't cancel if the action can be completed soon enough.
    }
    //FIXME
    DebugEx.Warning("*** {0}: Cancel action due to need turned critical: id={1}, timeToDestination={2}",
                    Character.FirstName, arg.NeedSpecification.Id, timeToDestination);
    CancelCurrentBehavior();
  }

  public bool CheckCanCancelCurrentBehavior() {
    return BehaviorManager._runningBehavior is CarryRootBehavior or AttractionNeedBehavior;
  }

  public float TimeToDestination() {
    var destination = Walker._pathCorners[Walker._pathCorners.Count - 1];
    return Walker.CalculateTravelTimeInHours(TransformFast.position, destination);
  }

  public void CancelCurrentBehavior() {
    var behavior = BehaviorManager._runningBehavior;
    switch (behavior) {
      case CarryRootBehavior carryBehavior:
        CancelCarryRootBehavior(carryBehavior);
        break;
      case AttractionNeedBehavior attractionBehavior:
        CancelAttractionBehavior(attractionBehavior);
        break;
    }
  }

  public Vector3 GetEssentialPosition() {
    return TransformFast.position;
  }

  public bool IsHauler() {
    if (!_worker) {
      return false;
    }
    if (!_worker.Workplace) {
      return false;  // Unemployed?
    }
    var behaviors = _worker.Workplace.WorkplaceBehaviors.Select(x => x.ComponentName).ToList();
    return behaviors.Any(x => x == HaulWorkplaceBehaviorName);
  }

  public bool IsBuilder() {
    if (!_worker) {
      return false;
    }
    if (!_worker.Workplace) {
      return false;  // Unemployed?
    }
    var behaviors = _worker.Workplace.WorkplaceBehaviors.Select(x => x.ComponentName).ToList();
    return behaviors.Any(x => x == BuilderHubWorkplaceBehaviorName);
  }

  void AbortCurrentBehavior(Walker walker) {
    var behavior = BehaviorManager._runningBehavior;
    var executor = BehaviorManager._runningExecutor;
    var behaviorName = behavior ? behavior.ComponentName + DebugEx.ObjectToString(behavior) : "NULL";
    var executorName = executor != null ? executor.GetName() + DebugEx.ObjectToString(executor) : "NULL";
    DebugEx.Warning("*** Aborting: behavior={0}, executor={1}", behaviorName, executorName);
    if (walker) {
      walker.Stop();
    }
    BehaviorManager._runningBehavior = null;
    BehaviorManager._runningExecutor = null;
  }

  void CancelCarryRootBehavior(CarryRootBehavior behavior) {
    if (behavior._goodCarrier.IsCarrying) {
      var coord = FixedWorldToGridInt(TransformFast.position);
      DebugEx.Warning("*** dropping carried good: name={0}, amount={1}, pos={2}, coords={3}",
                      behavior._goodCarrier.CarriedGoods.GoodId, behavior._goodCarrier.CarriedGoods.Amount,
                      TransformFast.position, coord);
      // FIXME: get it via injections.
      var stackSpawner = DependencyContainer.GetInstance<RecoveredGoodStackSpawner>();
      stackSpawner.AddAwaitingGoods(coord, new []{ behavior._goodCarrier.CarriedGoods});
      behavior._goodCarrier.EmptyHands();
    }
    if (behavior._goodReserver.HasReservedStock) {
      var good = behavior._goodReserver.StockReservation.GoodAmount;
      behavior._goodReserver.UnreserveStock();
    }
    if (behavior._goodReserver.HasReservedCapacity) {
      var good = behavior._goodReserver.CapacityReservation.GoodAmount;
      behavior._goodReserver.UnreserveCapacity();
    }
    AbortCurrentBehavior(behavior._walkToAccessibleExecutor._walker);
  }

  void CancelAttractionBehavior(AttractionNeedBehavior behavior) {
    var executor = GetComponentFast<WalkInsideExecutor>();
    executor._enterer.UnreserveSlot();
    AbortCurrentBehavior(executor._walker);
  }

  static Vector3Int FixedWorldToGridInt(Vector3 v) {
    return new Vector3(v.x + 0.1f, v.z + 0.1f, v.y + 0.1f).FloorToInt();
  }
}

}

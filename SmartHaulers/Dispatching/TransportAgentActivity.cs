// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BehaviorSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.Carrying;
using Timberborn.DwellingSystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SleepSystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportAgentActivity {
  const string WanderRootBehaviorName = "WanderRootBehavior";
  const string WaitInsideIdlyWorkplaceBehaviorName = "WaitInsideIdlyWorkplaceBehavior";
  const string CarryRootBehaviorName = "CarryRootBehavior";
  const string BringNutrientBehaviorName = "BringNutrientBehavior";
  const string NeedBehaviorSuffix = "NeedBehavior";

  public static readonly TransportAgentActivity Idle = new TransportAgentActivity(
      TransportAgentState.Available, isCarrying: false, hasStockReservation: false, hasCapacityReservation: false,
      jobRunning: false, new GoodAmount(null, 0), default, default, null, null, 0f, null, null);

  public TransportAgentState State { get; }
  public bool IsCarrying { get; }
  public bool HasStockReservation { get; }
  public bool HasCapacityReservation { get; }
  public bool JobRunning { get; }
  public GoodAmount CarriedGood { get; }
  public GoodReservation StockReservation { get; }
  public GoodReservation CapacityReservation { get; }
  public string BehaviorName { get; }
  public string ExecutorName { get; }
  public float ExecutorElapsedTime { get; }
  public string TargetLabel { get; }
  public BaseComponent Target { get; }
  public bool HasTargetInfo => !string.IsNullOrEmpty(TargetLabel);

  TransportAgentActivity(
      TransportAgentState state, bool isCarrying, bool hasStockReservation, bool hasCapacityReservation,
      bool jobRunning, GoodAmount carriedGood, GoodReservation stockReservation,
      GoodReservation capacityReservation, string behaviorName, string executorName, float executorElapsedTime,
      string targetLabel, BaseComponent target) {
    State = state;
    IsCarrying = isCarrying;
    HasStockReservation = hasStockReservation;
    HasCapacityReservation = hasCapacityReservation;
    JobRunning = jobRunning;
    CarriedGood = carriedGood;
    StockReservation = stockReservation;
    CapacityReservation = capacityReservation;
    BehaviorName = behaviorName;
    ExecutorName = executorName;
    ExecutorElapsedTime = executorElapsedTime;
    TargetLabel = targetLabel;
    Target = target;
  }

  public static TransportAgentActivity Create(
      GoodCarrier goodCarrier, GoodReserver goodReserver, BehaviorManager behaviorManager, bool jobRunning) {
    var runningBehavior = behaviorManager?.RunningBehavior.Name;
    var runningExecutor = behaviorManager?.RunningExecutor ?? default;
    var state = Classify(goodCarrier, goodReserver, runningBehavior);
    var target = GetTarget(behaviorManager, out var targetLabel);
    return new TransportAgentActivity(
        state, goodCarrier.IsCarrying, goodReserver.HasReservedStock, goodReserver.HasReservedCapacity,
        jobRunning, goodCarrier.CarriedGood.GoodAmount, goodReserver.StockReservation,
        goodReserver.CapacityReservation, runningBehavior, runningExecutor.Name, runningExecutor.ElapsedTime,
        targetLabel, target);
  }

  static BaseComponent GetTarget(BehaviorManager behaviorManager, out string targetLabel) {
    if (behaviorManager?._runningBehavior is SleepNeedBehavior sleepNeedBehavior) {
      if (sleepNeedBehavior._dweller.HasHome) {
        targetLabel = "sleepAt";
        return sleepNeedBehavior._dweller.Home;
      }
      targetLabel = "sleepOutside";
      return null;
    }
    targetLabel = null;
    return null;
  }

  static TransportAgentState Classify(GoodCarrier goodCarrier, GoodReserver goodReserver, string runningBehavior) {
    if (goodCarrier.IsCarrying
        || (HasReservation(goodReserver) && IsTransportBehavior(runningBehavior))) {
      return TransportAgentState.Transporting;
    }
    return runningBehavior switch {
        null => TransportAgentState.Available,
        WanderRootBehaviorName => TransportAgentState.IdleWandering,
        WaitInsideIdlyWorkplaceBehaviorName => TransportAgentState.WorkplaceIdle,
        var behaviorName when IsNeedBehavior(behaviorName) => TransportAgentState.SatisfyingNeed,
        _ => TransportAgentState.Working,
    };
  }

  static bool HasReservation(GoodReserver goodReserver) {
    return goodReserver.HasReservedStock || goodReserver.HasReservedCapacity;
  }

  static bool IsTransportBehavior(string runningBehavior) {
    return runningBehavior is CarryRootBehaviorName
        or BringNutrientBehaviorName
        or "BringNutrientWorkplaceBehavior"
        or "EmptyInventoriesWorkplaceBehavior"
        or "EmptyOutputWorkplaceBehavior"
        or "FillInputWorkplaceBehavior"
        or "ObtainGoodWorkplaceBehavior"
        or "RemoveUnwantedStockWorkplaceBehavior"
        or "SupplyGoodWorkplaceBehavior";
  }

  static bool IsNeedBehavior(string runningBehavior) {
    return runningBehavior?.EndsWith(NeedBehaviorSuffix) ?? false;
  }

  public override string ToString() {
    var targetText = HasTargetInfo
        ? Target ? $" {TargetLabel}={TransportDebugFormatter.FormatObject(Target)}" : $" {TargetLabel}"
        : "";
    if (State != TransportAgentState.Transporting && !string.IsNullOrEmpty(ExecutorName)) {
      return $"{BehaviorName}/{ExecutorName} {ExecutorElapsedTime:0.#}s{targetText}";
    }
    if (State != TransportAgentState.Transporting && !string.IsNullOrEmpty(BehaviorName)) {
      return $"{BehaviorName}{targetText}";
    }
    if (IsCarrying) {
      return $"Carrying {CarriedGood}";
    }
    if (HasStockReservation && HasCapacityReservation) {
      return $"Reserved {StockReservation.GoodAmount}";
    }
    if (HasStockReservation) {
      return $"Reserved stock {StockReservation.GoodAmount}";
    }
    if (HasCapacityReservation) {
      return $"Reserved capacity {CapacityReservation.GoodAmount}";
    }
    if (!string.IsNullOrEmpty(ExecutorName)) {
      return $"{BehaviorName}/{ExecutorName} {ExecutorElapsedTime:0.#}s";
    }
    return !string.IsNullOrEmpty(BehaviorName) ? BehaviorName : State.ToString();
  }
}

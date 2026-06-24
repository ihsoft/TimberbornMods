// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.Goods;
using Timberborn.InventorySystem;

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportAgentActivity {
  const string WanderRootBehaviorName = "WanderRootBehavior";
  const string WaitInsideIdlyWorkplaceBehaviorName = "WaitInsideIdlyWorkplaceBehavior";
  const string CarryRootBehaviorName = "CarryRootBehavior";
  const string NeedBehaviorSuffix = "NeedBehavior";

  public static readonly TransportAgentActivity Idle = new TransportAgentActivity(
      TransportAgentState.Available, isCarrying: false, hasStockReservation: false, hasCapacityReservation: false,
      jobRunning: false, new GoodAmount(null, 0), default, default, null, null, 0f);

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

  TransportAgentActivity(
      TransportAgentState state, bool isCarrying, bool hasStockReservation, bool hasCapacityReservation,
      bool jobRunning, GoodAmount carriedGood, GoodReservation stockReservation,
      GoodReservation capacityReservation, string behaviorName, string executorName, float executorElapsedTime) {
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
  }

  public static TransportAgentActivity Create(
      GoodCarrier goodCarrier, GoodReserver goodReserver, BehaviorManager behaviorManager, bool jobRunning) {
    var runningBehavior = behaviorManager?.RunningBehavior.Name;
    var runningExecutor = behaviorManager?.RunningExecutor ?? default;
    var state = Classify(goodCarrier, goodReserver, runningBehavior);
    return new TransportAgentActivity(
        state, goodCarrier.IsCarrying, goodReserver.HasReservedStock, goodReserver.HasReservedCapacity,
        jobRunning, goodCarrier.CarriedGood.GoodAmount, goodReserver.StockReservation,
        goodReserver.CapacityReservation, runningBehavior, runningExecutor.Name, runningExecutor.ElapsedTime);
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
    if (State != TransportAgentState.Transporting && !string.IsNullOrEmpty(ExecutorName)) {
      return $"{BehaviorName}/{ExecutorName} {ExecutorElapsedTime:0.#}s";
    }
    if (State != TransportAgentState.Transporting && !string.IsNullOrEmpty(BehaviorName)) {
      return BehaviorName;
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

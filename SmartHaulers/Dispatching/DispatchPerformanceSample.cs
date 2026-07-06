// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct DispatchPerformanceSample {
  public long TotalTicks { get; }
  public long AgentTicks { get; }
  public long ActiveOrderTicks { get; }
  public long QueuedOrderTicks { get; }
  public long ConstructionOrderTicks { get; }
  public long ReadinessTicks { get; }
  public long DecisionTicks { get; }
  public long SortTicks { get; }
  public long PickupPathTicks { get; }
  public long DeliveryPathTicks { get; }
  public long ActiveRoutePathTicks { get; }
  public long PlannerRoutePathTicks { get; }
  public long DecisionRoutePathTicks { get; }
  public long DecisionPickupPathTicks { get; }
  public long RemainingPathTicks { get; }
  public long RemainingPickupPathTicks { get; }
  public long RemainingDeliveryPathTicks { get; }
  public int AgentCount { get; }
  public int ActiveOrderCount { get; }
  public int QueuedOrderCount { get; }
  public int ConstructionOrderCount { get; }
  public int DecisionOrderCount { get; }
  public int DecisionCandidateCount { get; }
  public int PickupPathCalls { get; }
  public int DeliveryPathCalls { get; }
  public int ActiveRoutePathCalls { get; }
  public int PlannerRoutePathCalls { get; }
  public int DecisionRoutePathCalls { get; }
  public int DecisionPickupPathCalls { get; }
  public int RemainingPathCalls { get; }
  public int RemainingPickupPathCalls { get; }
  public int RemainingDeliveryPathCalls { get; }
  public int RouteCacheHits { get; }
  public int RouteCacheMisses { get; }
  public int RouteCacheClears { get; }

  public DispatchPerformanceSample(
      long totalTicks,
      long agentTicks,
      long activeOrderTicks,
      long queuedOrderTicks,
      long constructionOrderTicks,
      long readinessTicks,
      long decisionTicks,
      long sortTicks,
      long pickupPathTicks,
      long deliveryPathTicks,
      long activeRoutePathTicks,
      long plannerRoutePathTicks,
      long decisionRoutePathTicks,
      long decisionPickupPathTicks,
      long remainingPathTicks,
      long remainingPickupPathTicks,
      long remainingDeliveryPathTicks,
      int agentCount,
      int activeOrderCount,
      int queuedOrderCount,
      int constructionOrderCount,
      int decisionOrderCount,
      int decisionCandidateCount,
      int pickupPathCalls,
      int deliveryPathCalls,
      int activeRoutePathCalls,
      int plannerRoutePathCalls,
      int decisionRoutePathCalls,
      int decisionPickupPathCalls,
      int remainingPathCalls,
      int remainingPickupPathCalls,
      int remainingDeliveryPathCalls,
      int routeCacheHits,
      int routeCacheMisses,
      int routeCacheClears) {
    TotalTicks = totalTicks;
    AgentTicks = agentTicks;
    ActiveOrderTicks = activeOrderTicks;
    QueuedOrderTicks = queuedOrderTicks;
    ConstructionOrderTicks = constructionOrderTicks;
    ReadinessTicks = readinessTicks;
    DecisionTicks = decisionTicks;
    SortTicks = sortTicks;
    PickupPathTicks = pickupPathTicks;
    DeliveryPathTicks = deliveryPathTicks;
    ActiveRoutePathTicks = activeRoutePathTicks;
    PlannerRoutePathTicks = plannerRoutePathTicks;
    DecisionRoutePathTicks = decisionRoutePathTicks;
    DecisionPickupPathTicks = decisionPickupPathTicks;
    RemainingPathTicks = remainingPathTicks;
    RemainingPickupPathTicks = remainingPickupPathTicks;
    RemainingDeliveryPathTicks = remainingDeliveryPathTicks;
    AgentCount = agentCount;
    ActiveOrderCount = activeOrderCount;
    QueuedOrderCount = queuedOrderCount;
    ConstructionOrderCount = constructionOrderCount;
    DecisionOrderCount = decisionOrderCount;
    DecisionCandidateCount = decisionCandidateCount;
    PickupPathCalls = pickupPathCalls;
    DeliveryPathCalls = deliveryPathCalls;
    ActiveRoutePathCalls = activeRoutePathCalls;
    PlannerRoutePathCalls = plannerRoutePathCalls;
    DecisionRoutePathCalls = decisionRoutePathCalls;
    DecisionPickupPathCalls = decisionPickupPathCalls;
    RemainingPathCalls = remainingPathCalls;
    RemainingPickupPathCalls = remainingPickupPathCalls;
    RemainingDeliveryPathCalls = remainingDeliveryPathCalls;
    RouteCacheHits = routeCacheHits;
    RouteCacheMisses = routeCacheMisses;
    RouteCacheClears = routeCacheClears;
  }

  public DispatchPerformanceSample Add(DispatchPerformanceSample other) {
    return new DispatchPerformanceSample(
        TotalTicks + other.TotalTicks,
        AgentTicks + other.AgentTicks,
        ActiveOrderTicks + other.ActiveOrderTicks,
        QueuedOrderTicks + other.QueuedOrderTicks,
        ConstructionOrderTicks + other.ConstructionOrderTicks,
        ReadinessTicks + other.ReadinessTicks,
        DecisionTicks + other.DecisionTicks,
        SortTicks + other.SortTicks,
        PickupPathTicks + other.PickupPathTicks,
        DeliveryPathTicks + other.DeliveryPathTicks,
        ActiveRoutePathTicks + other.ActiveRoutePathTicks,
        PlannerRoutePathTicks + other.PlannerRoutePathTicks,
        DecisionRoutePathTicks + other.DecisionRoutePathTicks,
        DecisionPickupPathTicks + other.DecisionPickupPathTicks,
        RemainingPathTicks + other.RemainingPathTicks,
        RemainingPickupPathTicks + other.RemainingPickupPathTicks,
        RemainingDeliveryPathTicks + other.RemainingDeliveryPathTicks,
        AgentCount + other.AgentCount,
        ActiveOrderCount + other.ActiveOrderCount,
        QueuedOrderCount + other.QueuedOrderCount,
        ConstructionOrderCount + other.ConstructionOrderCount,
        DecisionOrderCount + other.DecisionOrderCount,
        DecisionCandidateCount + other.DecisionCandidateCount,
        PickupPathCalls + other.PickupPathCalls,
        DeliveryPathCalls + other.DeliveryPathCalls,
        ActiveRoutePathCalls + other.ActiveRoutePathCalls,
        PlannerRoutePathCalls + other.PlannerRoutePathCalls,
        DecisionRoutePathCalls + other.DecisionRoutePathCalls,
        DecisionPickupPathCalls + other.DecisionPickupPathCalls,
        RemainingPathCalls + other.RemainingPathCalls,
        RemainingPickupPathCalls + other.RemainingPickupPathCalls,
        RemainingDeliveryPathCalls + other.RemainingDeliveryPathCalls,
        RouteCacheHits + other.RouteCacheHits,
        RouteCacheMisses + other.RouteCacheMisses,
        RouteCacheClears + other.RouteCacheClears);
  }
}

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
  public int PickupPathCalls { get; }
  public int DeliveryPathCalls { get; }

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
      int pickupPathCalls,
      int deliveryPathCalls) {
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
    PickupPathCalls = pickupPathCalls;
    DeliveryPathCalls = deliveryPathCalls;
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
        PickupPathCalls + other.PickupPathCalls,
        DeliveryPathCalls + other.DeliveryPathCalls);
  }
}

// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct DispatchPerformanceSample {
  public long TotalTicks { get; }
  public long PickupPathTicks { get; }
  public long DeliveryPathTicks { get; }
  public int PickupPathCalls { get; }
  public int DeliveryPathCalls { get; }

  public DispatchPerformanceSample(
      long totalTicks, long pickupPathTicks, long deliveryPathTicks, int pickupPathCalls, int deliveryPathCalls) {
    TotalTicks = totalTicks;
    PickupPathTicks = pickupPathTicks;
    DeliveryPathTicks = deliveryPathTicks;
    PickupPathCalls = pickupPathCalls;
    DeliveryPathCalls = deliveryPathCalls;
  }

  public DispatchPerformanceSample Add(DispatchPerformanceSample other) {
    return new DispatchPerformanceSample(
        TotalTicks + other.TotalTicks,
        PickupPathTicks + other.PickupPathTicks,
        DeliveryPathTicks + other.DeliveryPathTicks,
        PickupPathCalls + other.PickupPathCalls,
        DeliveryPathCalls + other.DeliveryPathCalls);
  }
}

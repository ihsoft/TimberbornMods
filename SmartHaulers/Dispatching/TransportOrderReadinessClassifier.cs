// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportOrderReadinessClassifier {
  const float UrgentDeliveryThresholdInHours = 4f;
  const float RelaxedDeliveryThresholdMultiplier = 3f;
  const float RelaxedDeliveryThresholdInHours = UrgentDeliveryThresholdInHours * RelaxedDeliveryThresholdMultiplier;

  // Fallback for order types where the prototype cannot estimate time-to-blockage yet.
  const float LowStockDispatchThreshold = 0.5f;
  const float HighStockDispatchThreshold = 0.5f;

  public static TransportOrderSnapshot Classify(TransportOrderSnapshot order) {
    if (order.Phase != OrderPhase.Estimated) {
      return order;
    }
    if (TransportOrderCriticalTimeEstimator.TryGetHoursUntilCritical(order, out var criticalTimeInHours)) {
      return order
          .WithCriticalTime(criticalTimeInHours)
          .WithPhase(TimeReadinessPhase(criticalTimeInHours));
    }
    return IsDispatchable(order)
        ? order.WithPhase(OrderPhase.Dispatchable)
        : order.WithPhase(OrderPhase.Deferred);
  }

  static OrderPhase TimeReadinessPhase(float criticalTimeInHours) {
    if (criticalTimeInHours <= UrgentDeliveryThresholdInHours) {
      return OrderPhase.Dispatchable;
    }
    return criticalTimeInHours <= RelaxedDeliveryThresholdInHours ? OrderPhase.Estimated : OrderPhase.Deferred;
  }

  static bool IsDispatchable(TransportOrderSnapshot order) {
    if (order.Origin.Type == TransportOrderOriginType.ConstructionJob) {
      return true;
    }
    if (!order.Cargo.HasGoods) {
      return false;
    }
    if (IsBringBehavior(order.BehaviorName)) {
      return TryGetFillRatio(order.Target, order.Cargo.GoodId, out var fillRatio)
          && fillRatio <= LowStockDispatchThreshold;
    }
    if (IsTakeAwayBehavior(order.BehaviorName)) {
      return TryGetFillRatio(order.Source, order.Cargo.GoodId, out var fillRatio)
          && fillRatio >= HighStockDispatchThreshold;
    }
    return false;
  }

  static bool TryGetFillRatio(Timberborn.InventorySystem.Inventory inventory, string goodId, out float fillRatio) {
    fillRatio = 0f;
    if (!inventory) {
      return false;
    }
    var limit = inventory.LimitedAmount(goodId);
    if (limit <= 0) {
      fillRatio = inventory.AmountInStock(goodId) > 0 ? 1f : 0f;
      return true;
    }
    fillRatio = (float)inventory.AmountInStock(goodId) / limit;
    return true;
  }

  static bool IsBringBehavior(string behaviorName) {
    return behaviorName is "BringNutrient" or "FillInput" or "ObtainGood";
  }

  static bool IsTakeAwayBehavior(string behaviorName) {
    return behaviorName is "EmptyInventories" or "EmptyOutput" or "RemoveUnwantedStock" or "SupplyGood";
  }
}

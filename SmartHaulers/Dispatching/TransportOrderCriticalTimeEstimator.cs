// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Workshops;

namespace IgorZ.SmartHaulers.Dispatching;

static class TransportOrderCriticalTimeEstimator {
  public static bool TryGetHoursUntilCritical(TransportOrderSnapshot order, out float hours) {
    hours = float.NaN;
    if (!order.Cargo.HasGoods) {
      return false;
    }
    if (order.BehaviorName == "FillInput") {
      return TryGetBringHoursUntilCritical(order.Target, order.Cargo.GoodId, out hours);
    }
    if (order.BehaviorName == "EmptyOutput") {
      return TryGetTakeAwayHoursUntilCritical(order.Source, order.Cargo.GoodId, out hours);
    }
    return false;
  }

  static bool TryGetBringHoursUntilCritical(Inventory target, string goodId, out float hours) {
    hours = float.NaN;
    if (!target) {
      return false;
    }
    var manufactory = target.GetComponent<Manufactory>();
    if (manufactory && TryGetManufactoryInputHours(manufactory, goodId, out hours)) {
      return true;
    }
    var goodConsumingBuilding = target.GetComponent<GoodConsumingBuilding>();
    return goodConsumingBuilding && TryGetGoodConsumingBuildingHours(goodConsumingBuilding, goodId, out hours);
  }

  static bool TryGetTakeAwayHoursUntilCritical(Inventory source, string goodId, out float hours) {
    hours = float.NaN;
    if (!source) {
      return false;
    }
    var manufactory = source.GetComponent<Manufactory>();
    return manufactory && TryGetManufactoryOutputHours(manufactory, goodId, out hours);
  }

  static bool TryGetManufactoryInputHours(Manufactory manufactory, string goodId, out float hours) {
    hours = float.NaN;
    if (!manufactory.HasCurrentRecipe) {
      return false;
    }
    foreach (var ingredient in manufactory.CurrentRecipe.Ingredients) {
      if (ingredient.Id == goodId) {
        hours = IngredientHoursUntilMissing(manufactory, ingredient);
        return true;
      }
    }
    if (manufactory.CurrentRecipe.ConsumesFuel && manufactory.CurrentRecipe.Fuel == goodId) {
      hours = FuelHoursUntilMissing(manufactory);
      return true;
    }
    return false;
  }

  static bool TryGetManufactoryOutputHours(Manufactory manufactory, string goodId, out float hours) {
    hours = float.NaN;
    if (!manufactory.HasCurrentRecipe) {
      return false;
    }
    foreach (var product in manufactory.CurrentRecipe.Products) {
      if (product.Id == goodId) {
        hours = ProductHoursUntilBlocked(manufactory, product);
        return true;
      }
    }
    return false;
  }

  static bool TryGetGoodConsumingBuildingHours(
      GoodConsumingBuilding goodConsumingBuilding, string goodId, out float hours) {
    hours = float.NaN;
    for (var i = 0; i < goodConsumingBuilding.ConsumedGoods.Length; i++) {
      var consumedGood = goodConsumingBuilding.ConsumedGoods[i];
      if (consumedGood.GoodId != goodId || consumedGood.GoodPerHour <= 0f) {
        continue;
      }
      hours = (goodConsumingBuilding.Inventory.UnreservedAmountInStock(goodId)
          + goodConsumingBuilding._suppliesLeft[i]) / consumedGood.GoodPerHour;
      return true;
    }
    return false;
  }

  static float IngredientHoursUntilMissing(Manufactory manufactory, GoodAmountSpec ingredient) {
    if (ingredient.Amount <= 0) {
      return float.MaxValue;
    }
    var futureCycles = manufactory.Inventory.UnreservedAmountInStock(ingredient.Id) / ingredient.Amount;
    var currentCycleHours = manufactory._ingredientsConsumed ? RemainingCycleHours(manufactory) : 0f;
    return currentCycleHours + futureCycles * manufactory.CurrentRecipe.CycleDurationInHours;
  }

  static float FuelHoursUntilMissing(Manufactory manufactory) {
    var recipe = manufactory.CurrentRecipe;
    if (recipe.CyclesFuelLasts <= 0) {
      return float.MaxValue;
    }
    var storedFuelCycles = manufactory.Inventory.UnreservedAmountInStock(recipe.Fuel) * recipe.CyclesFuelLasts;
    var loadedFuelCycles = manufactory.FuelRemaining * recipe.CyclesFuelLasts;
    var remainingFuelCycles = storedFuelCycles + loadedFuelCycles - manufactory.ProductionProgress;
    return Math.Max(0f, remainingFuelCycles) * recipe.CycleDurationInHours;
  }

  static float ProductHoursUntilBlocked(Manufactory manufactory, GoodAmountSpec product) {
    if (product.Amount <= 0) {
      return float.MaxValue;
    }
    var futureCycles = manufactory.Inventory.UnreservedCapacity(product.Id) / product.Amount;
    if (futureCycles <= 0) {
      return 0f;
    }
    return RemainingCycleHours(manufactory) + (futureCycles - 1) * manufactory.CurrentRecipe.CycleDurationInHours;
  }

  static float RemainingCycleHours(Manufactory manufactory) {
    var remainingProgress = Math.Max(0f, 1f - manufactory.ProductionProgress);
    return remainingProgress * manufactory.CurrentRecipe.CycleDurationInHours;
  }
}

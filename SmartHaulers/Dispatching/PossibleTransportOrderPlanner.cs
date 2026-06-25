// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.Emptying;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.Hauling;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.Reproduction;
using Timberborn.StockpilePrioritySystem;
using Timberborn.Stockpiles;
using Timberborn.WorkSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartHaulers.Dispatching;

static class PossibleTransportOrderPlanner {
  const string BringNutrientBehaviorName = "BringNutrient";
  const string EmptyInventoriesBehaviorName = "EmptyInventories";
  const string EmptyOutputBehaviorName = "EmptyOutput";
  const string FillInputBehaviorName = "FillInput";
  const string ObtainGoodBehaviorName = "ObtainGood";
  const string RemoveUnwantedStockBehaviorName = "RemoveUnwantedStock";
  const string SupplyGoodBehaviorName = "SupplyGood";
  const string WorkplaceBehaviorSuffix = "WorkplaceBehavior";

  public static void EnsureSupported(WorkplaceBehavior workplaceBehavior, object context) {
    EnsureSupported(workplaceBehavior.GetType(), context);
  }

  public static void EnsureSupported(Type workplaceBehaviorType, object context) {
    var behaviorName = FormatBehaviorName(workplaceBehaviorType);
    if (IsSupported(behaviorName)) {
      return;
    }
    throw new NotSupportedException(
        $"Unsupported SmartHaulers haul behavior {behaviorName} on {DebugEx.ObjectToString(context)}.");
  }

  public static TransportOrderSnapshot Plan(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, WeightedBehavior weightedBehavior) {
    EnsureSupported(weightedBehavior.WorkplaceBehavior, haulCandidate);
    var behaviorName = FormatBehaviorName(weightedBehavior.WorkplaceBehavior);
    var requesterId = haulCandidate.GetComponent<EntityComponent>()?.EntityId ?? Guid.Empty;
    return behaviorName switch {
        BringNutrientBehaviorName => PlanBringNutrient(districtCenter, haulCandidate, requesterId, weightedBehavior),
        EmptyInventoriesBehaviorName => PlanEmptyInventories(districtCenter, haulCandidate, requesterId, weightedBehavior),
        EmptyOutputBehaviorName => PlanEmptyOutput(districtCenter, haulCandidate, requesterId, weightedBehavior),
        FillInputBehaviorName => PlanFillInput(districtCenter, haulCandidate, requesterId, weightedBehavior),
        ObtainGoodBehaviorName => PlanObtainGood(districtCenter, haulCandidate, requesterId, weightedBehavior),
        RemoveUnwantedStockBehaviorName => PlanRemoveUnwantedStock(
            districtCenter, haulCandidate, requesterId, weightedBehavior),
        SupplyGoodBehaviorName => PlanSupplyGood(districtCenter, haulCandidate, requesterId, weightedBehavior),
        _ => throw new InvalidOperationException($"Supported behavior {behaviorName} was not handled."),
    };
  }

  static bool IsSupported(string behaviorName) {
    return behaviorName is BringNutrientBehaviorName
        or EmptyInventoriesBehaviorName
        or EmptyOutputBehaviorName
        or FillInputBehaviorName
        or ObtainGoodBehaviorName
        or RemoveUnwantedStockBehaviorName
        or SupplyGoodBehaviorName;
  }

  static TransportOrderSnapshot PlanBringNutrient(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var breedingPod = haulCandidate.GetComponent<BreedingPod>();
    var target = breedingPod?.Inventory;
    if (!target) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
    }
    foreach (var nutrient in breedingPod.NutrientsPerCycle) {
      if (TryPlanBringingGood(districtCenter, target, nutrient.Id, _ => true, out var order)) {
        return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
      }
      if (TryCreateCoveredOrder(haulCandidate, requesterId, weightedBehavior, target, nutrient.Id, out var coveredOrder)) {
        return coveredOrder;
      }
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
  }

  static TransportOrderSnapshot PlanFillInput(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    var emptiable = haulCandidate.GetComponent<Emptiable>();
    if (!inventories || emptiable && emptiable.IsMarkedForEmptying) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null);
    }
    foreach (var target in inventories.EnabledInventories) {
      var inputGoodsOrdered = new SortedSet<GoodAmount>(new GoodAmountComparer());
      foreach (var inputGood in target.InputGoods) {
        inputGoodsOrdered.Add(new GoodAmount(inputGood, target.UnreservedAmountInStock(inputGood)));
      }
      foreach (var inputGood in inputGoodsOrdered) {
        if (TryPlanBringingGood(districtCenter, target, inputGood.GoodId, _ => true, out var order)) {
          return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
        }
        if (TryCreateCoveredOrder(haulCandidate, requesterId, weightedBehavior, target, inputGood.GoodId, out var coveredOrder)) {
          return coveredOrder;
        }
      }
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, PickInputInventory(inventories));
  }

  static TransportOrderSnapshot PlanObtainGood(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var target = haulCandidate.GetComponent<Stockpile>()?.Inventory;
    var singleGoodAllower = haulCandidate.GetComponent<SingleGoodAllower>();
    if (!target || !singleGoodAllower || !singleGoodAllower.HasAllowedGood) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
    }
    if (TryPlanBringingGood(districtCenter, target, singleGoodAllower.AllowedGood, CanObtainFrom, out var order)) {
      return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
    }
    if (TryCreateCoveredOrder(
        haulCandidate, requesterId, weightedBehavior, target, singleGoodAllower.AllowedGood, out var coveredOrder)) {
      return coveredOrder;
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
  }

  static TransportOrderSnapshot PlanEmptyInventories(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    var emptiable = haulCandidate.GetComponent<Emptiable>();
    if (!inventories || !emptiable || !emptiable.IsMarkedForEmptying) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null);
    }
    return PlanFirstTakeAwayOrder(districtCenter, haulCandidate, requesterId, weightedBehavior, inventories, GetUnreservedGoods);
  }

  static TransportOrderSnapshot PlanEmptyOutput(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    if (!inventories) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null);
    }
    return PlanFirstTakeAwayOrder(
        districtCenter, haulCandidate, requesterId, weightedBehavior, inventories,
        inventory => inventory.OutputGoods.Count > 0 ? GetUnreservedGoods(inventory) : EmptyGoods());
  }

  static TransportOrderSnapshot PlanRemoveUnwantedStock(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    if (!inventories) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null);
    }
    return PlanFirstTakeAwayOrder(
        districtCenter, haulCandidate, requesterId, weightedBehavior, inventories,
        inventory => inventory.HasUnwantedStock ? inventory.UnreservedUnwantedStock() : EmptyGoods());
  }

  static TransportOrderSnapshot PlanSupplyGood(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var source = haulCandidate.GetComponent<Stockpile>()?.Inventory;
    var singleGoodAllower = haulCandidate.GetComponent<SingleGoodAllower>();
    if (!source || !singleGoodAllower || !singleGoodAllower.HasAllowedGood) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source, target: null);
    }
    var goodAmount = new GoodAmount(
        singleGoodAllower.AllowedGood, source.UnreservedAmountInStock(singleGoodAllower.AllowedGood));
    if (TryPlanTakingGood(districtCenter, source, goodAmount, CanGiveTo, out var order)) {
      return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source, target: null);
  }

  static TransportOrderSnapshot PlanFirstTakeAwayOrder(
      DistrictCenter districtCenter,
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventories inventories,
      Func<Inventory, IEnumerable<GoodAmount>> goodsProvider) {
    foreach (var source in inventories.EnabledInventories) {
      foreach (var goodAmount in goodsProvider(source)) {
        if (TryPlanTakingGood(districtCenter, source, goodAmount, _ => true, out var order)) {
          return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
        }
      }
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, PickStockedInventory(inventories), target: null);
  }

  static bool TryPlanBringingGood(
      DistrictCenter districtCenter,
      Inventory target,
      string goodId,
      Predicate<Inventory> sourceFilter,
      out (Inventory source, Inventory target, GoodAmount goodAmount) order) {
    order = default;
    var targetAccessible = target.GetEnabledComponent<Accessible>();
    if (!targetAccessible) {
      return false;
    }
    var source = districtCenter.GetComponent<DistrictInventoryPicker>()
        .ClosestInventoryWithStock(targetAccessible, goodId, sourceFilter);
    if (!source) {
      return false;
    }
    var goodAmount = MaxTransferableAmount(source, target, goodId);
    if (goodAmount.Amount <= 0) {
      return false;
    }
    order = (source, target, goodAmount);
    return true;
  }

  static bool TryPlanTakingGood(
      DistrictCenter districtCenter,
      Inventory source,
      GoodAmount availableGood,
      Predicate<Inventory> targetFilter,
      out (Inventory source, Inventory target, GoodAmount goodAmount) order) {
    order = default;
    if (availableGood.Amount <= 0) {
      return false;
    }
    var sourceAccessible = source.GetEnabledComponent<Accessible>();
    if (!sourceAccessible) {
      return false;
    }
    var target = districtCenter.GetComponent<DistrictInventoryPicker>()
        .ClosestInventoryWithCapacity(sourceAccessible, new GoodAmount(availableGood.GoodId, 1), targetFilter, out _);
    if (!target) {
      return false;
    }
    var goodAmount = MaxTransferableAmount(source, target, availableGood);
    if (goodAmount.Amount <= 0) {
      return false;
    }
    order = (source, target, goodAmount);
    return true;
  }

  static GoodAmount MaxTransferableAmount(Inventory source, Inventory target, string goodId) {
    return MaxTransferableAmount(source, target, new GoodAmount(goodId, source.UnreservedAmountInStock(goodId)));
  }

  static GoodAmount MaxTransferableAmount(Inventory source, Inventory target, GoodAmount availableGood) {
    var amount = Math.Min(availableGood.Amount, target.UnreservedCapacity(availableGood.GoodId));
    return new GoodAmount(availableGood.GoodId, amount);
  }

  static IEnumerable<GoodAmount> GetUnreservedGoods(Inventory inventory) {
    var emptiable = inventory.GetComponent<Emptiable>();
    if (!emptiable || !emptiable.IsMarkedForEmptying) {
      return inventory.UnreservedTakeableStock();
    }
    return inventory.UnreservedStock();
  }

  static IEnumerable<GoodAmount> EmptyGoods() {
    return [];
  }

  static bool CanGiveTo(Inventory inventory) {
    var goodSupplier = inventory.GetComponent<GoodSupplier>();
    return !goodSupplier || !goodSupplier.IsSupplying;
  }

  static bool CanObtainFrom(Inventory inventory) {
    var goodObtainer = inventory.GetComponent<GoodObtainer>();
    return !goodObtainer || !goodObtainer.IsObtaining;
  }

  static Inventory PickInputInventory(Inventories inventories) {
    foreach (var inventory in inventories.EnabledInventories) {
      if (inventory.IsInput) {
        return inventory;
      }
    }
    return null;
  }

  static Inventory PickStockedInventory(Inventories inventories) {
    foreach (var inventory in inventories.EnabledInventories) {
      if (inventory.HasAnyUnreservedStock) {
        return inventory;
      }
    }
    return null;
  }

  static TransportOrderSnapshot WithRequest(
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventory source,
      Inventory target,
      GoodAmount goodAmount) {
    return TransportOrderSnapshot.Queued(
        requesterId, haulCandidate, FormatBehaviorName(weightedBehavior.WorkplaceBehavior), weightedBehavior.Weight,
        source, target, goodAmount);
  }

  static bool TryCreateCoveredOrder(
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventory target,
      string goodId,
      out TransportOrderSnapshot order) {
    var reservedCapacity = target.ReservedCapacity(goodId);
    if (reservedCapacity <= 0 || target.UnreservedCapacity(goodId) > 0) {
      order = default;
      return false;
    }
    order = TransportOrderSnapshot.Covered(
        requesterId, haulCandidate, FormatBehaviorName(weightedBehavior.WorkplaceBehavior), weightedBehavior.Weight,
        target, new GoodAmount(goodId, reservedCapacity));
    return true;
  }

  static TransportOrderSnapshot EmptyOrder(
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventory source,
      Inventory target) {
    return WithRequest(haulCandidate, requesterId, weightedBehavior, source, target, new GoodAmount(null, 0));
  }

  static string FormatBehaviorName(WorkplaceBehavior workplaceBehavior) {
    return FormatBehaviorName(workplaceBehavior.GetType());
  }

  static string FormatBehaviorName(Type workplaceBehaviorType) {
    var name = workplaceBehaviorType.Name;
    return name.EndsWith(WorkplaceBehaviorSuffix) ? name[..^WorkplaceBehaviorSuffix.Length] : name;
  }
}

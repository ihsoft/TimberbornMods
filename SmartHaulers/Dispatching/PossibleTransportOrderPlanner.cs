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
  // Prototype cap: large output inventories can have many possible targets. Three keeps diagnostics readable for now.
  const int MaxTakeAwayTargetCandidates = 3;

  readonly struct PlannedCandidate {
    public readonly Inventory Source;
    public readonly Inventory Target;
    public readonly GoodAmount GoodAmount;
    public readonly float Distance;

    public PlannedCandidate(Inventory source, Inventory target, GoodAmount goodAmount, float distance) {
      Source = source;
      Target = target;
      GoodAmount = goodAmount;
      Distance = distance;
    }
  }

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

  public static void AddPlans(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, WeightedBehavior weightedBehavior,
      List<TransportOrderSnapshot> orders) {
    EnsureSupported(weightedBehavior.WorkplaceBehavior, haulCandidate);
    var behaviorName = FormatBehaviorName(weightedBehavior.WorkplaceBehavior);
    var requesterId = haulCandidate.GetComponent<EntityComponent>()?.EntityId ?? Guid.Empty;
    if (behaviorName == FillInputBehaviorName) {
      AddFillInputPlans(districtCenter, haulCandidate, requesterId, weightedBehavior, orders);
      return;
    }
    if (behaviorName == EmptyInventoriesBehaviorName) {
      AddEmptyInventoriesPlans(districtCenter, haulCandidate, requesterId, weightedBehavior, orders);
      return;
    }
    if (behaviorName == EmptyOutputBehaviorName) {
      AddEmptyOutputPlans(districtCenter, haulCandidate, requesterId, weightedBehavior, orders);
      return;
    }
    if (behaviorName == RemoveUnwantedStockBehaviorName) {
      AddRemoveUnwantedStockPlans(districtCenter, haulCandidate, requesterId, weightedBehavior, orders);
      return;
    }
    orders.Add(behaviorName switch {
        BringNutrientBehaviorName => PlanBringNutrient(districtCenter, haulCandidate, requesterId, weightedBehavior),
        ObtainGoodBehaviorName => PlanObtainGood(districtCenter, haulCandidate, requesterId, weightedBehavior),
        SupplyGoodBehaviorName => PlanSupplyGood(districtCenter, haulCandidate, requesterId, weightedBehavior),
        _ => throw new InvalidOperationException($"Supported behavior {behaviorName} was not handled."),
    });
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
      if (TryPlanClosestBringingGood(districtCenter, target, nutrient.Id, IsAvailableSource, out var order)) {
        return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
      }
      if (TryCreateCoveredOrder(
          haulCandidate, requesterId, weightedBehavior, target, nutrient.Id, out var coveredOrder)) {
        return coveredOrder;
      }
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
  }

  static void AddFillInputPlans(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior,
      List<TransportOrderSnapshot> orders) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    var emptiable = haulCandidate.GetComponent<Emptiable>();
    if (!inventories || emptiable && emptiable.IsMarkedForEmptying) {
      orders.Add(EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null));
      return;
    }
    var added = false;
    foreach (var target in inventories.EnabledInventories) {
      foreach (var inputGood in target.InputGoods) {
        var weight = InputGoodWeight(target, inputGood, weightedBehavior.Weight);
        var addedGood = false;
        foreach (var order in PlanBringingGoodCandidates(districtCenter, target, inputGood, IsAvailableSource)) {
          orders.Add(WithRequest(
              haulCandidate, requesterId, weightedBehavior, weight, order.source, order.target, order.goodAmount));
          added = true;
          addedGood = true;
        }
        if (!addedGood && TryCreateCoveredOrder(
            haulCandidate, requesterId, weightedBehavior, weight, target, inputGood, out var coveredOrder)) {
          orders.Add(coveredOrder);
          added = true;
        }
      }
    }
    if (!added) {
      orders.Add(EmptyOrder(
          haulCandidate, requesterId, weightedBehavior, source: null, PickInputInventory(inventories)));
    }
  }

  static TransportOrderSnapshot PlanObtainGood(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior) {
    var target = haulCandidate.GetComponent<Stockpile>()?.Inventory;
    var singleGoodAllower = haulCandidate.GetComponent<SingleGoodAllower>();
    if (!target || !singleGoodAllower || !singleGoodAllower.HasAllowedGood) {
      return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
    }
    if (TryPlanClosestBringingGood(
        districtCenter, target, singleGoodAllower.AllowedGood, CanObtainFrom, out var order)) {
      return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
    }
    if (TryCreateCoveredOrder(
        haulCandidate, requesterId, weightedBehavior, target, singleGoodAllower.AllowedGood, out var coveredOrder)) {
      return coveredOrder;
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target);
  }

  static void AddEmptyInventoriesPlans(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior,
      List<TransportOrderSnapshot> orders) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    var emptiable = haulCandidate.GetComponent<Emptiable>();
    if (!inventories || !emptiable || !emptiable.IsMarkedForEmptying) {
      orders.Add(EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null));
      return;
    }
    AddTakeAwayPlans(districtCenter, haulCandidate, requesterId, weightedBehavior, inventories, GetUnreservedGoods,
        orders);
  }

  static void AddEmptyOutputPlans(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior,
      List<TransportOrderSnapshot> orders) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    if (!inventories) {
      orders.Add(EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null));
      return;
    }
    AddTakeAwayPlans(
        districtCenter, haulCandidate, requesterId, weightedBehavior, inventories, GetUnreservedOutputGoods, orders);
  }

  static void AddRemoveUnwantedStockPlans(
      DistrictCenter districtCenter, HaulCandidate haulCandidate, Guid requesterId, WeightedBehavior weightedBehavior,
      List<TransportOrderSnapshot> orders) {
    var inventories = haulCandidate.GetComponent<Inventories>();
    if (!inventories) {
      orders.Add(EmptyOrder(haulCandidate, requesterId, weightedBehavior, source: null, target: null));
      return;
    }
    AddTakeAwayPlans(
        districtCenter, haulCandidate, requesterId, weightedBehavior, inventories, GetUnreservedUnwantedGoods,
        orders);
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
    if (TryPlanTakingGood(
        districtCenter,
        source,
        goodAmount,
        inventory => IsAvailableTarget(inventory) && CanGiveTo(inventory),
        out var order)) {
      return WithRequest(haulCandidate, requesterId, weightedBehavior, order.source, order.target, order.goodAmount);
    }
    return EmptyOrder(haulCandidate, requesterId, weightedBehavior, source, target: null);
  }

  static void AddTakeAwayPlans(
      DistrictCenter districtCenter,
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventories inventories,
      Func<Inventory, IEnumerable<GoodAmount>> goodsProvider,
      List<TransportOrderSnapshot> orders) {
    var added = false;
    foreach (var source in inventories.EnabledInventories) {
      foreach (var goodAmount in goodsProvider(source)) {
        foreach (var order in PlanTakingGoodCandidates(districtCenter, source, goodAmount, IsAvailableTarget)) {
          var weight = TakeAwayGoodWeight(source, goodAmount.GoodId, weightedBehavior.Weight);
          orders.Add(WithRequest(
              haulCandidate, requesterId, weightedBehavior, weight, order.source, order.target, order.goodAmount));
          added = true;
        }
      }
    }
    if (!added) {
      orders.Add(EmptyOrder(haulCandidate, requesterId, weightedBehavior, PickStockedInventory(inventories), null));
    }
  }

  static bool TryPlanClosestBringingGood(
      DistrictCenter districtCenter,
      Inventory target,
      string goodId,
      Predicate<Inventory> sourceFilter,
      out (Inventory source, Inventory target, GoodAmount goodAmount) order) {
    order = default;
    var targetAccessible = target.GetEnabledComponent<Accessible>();
    if (!targetAccessible || !IsAvailableTarget(target)) {
      return false;
    }
    var source = districtCenter.GetComponent<DistrictInventoryPicker>()
        .ClosestInventoryWithStock(
            targetAccessible, goodId, inventory => IsAvailableSource(inventory) && sourceFilter(inventory));
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

  static IEnumerable<(Inventory source, Inventory target, GoodAmount goodAmount)> PlanBringingGoodCandidates(
      DistrictCenter districtCenter,
      Inventory target,
      string goodId,
      Predicate<Inventory> sourceFilter) {
    var targetAccessible = target.GetEnabledComponent<Accessible>();
    if (!targetAccessible || !IsAvailableTarget(target)) {
      yield break;
    }
    var requestedAmount = target.UnreservedCapacity(goodId);
    if (requestedAmount <= 0) {
      yield break;
    }
    var candidates = new List<PlannedCandidate>();
    var districtInventoryRegistry = districtCenter.GetComponent<DistrictInventoryRegistry>();
    foreach (var source in districtInventoryRegistry.ActiveInventoriesWithStock(goodId)) {
      var sourceAccessible = source.GetEnabledComponent<Accessible>();
      if (!sourceAccessible
          || !IsAvailableSource(source)
          || !sourceFilter(source)
          || !targetAccessible.FindRoadPath(sourceAccessible, out var distance)) {
        continue;
      }
      var goodAmount = MaxTransferableAmount(source, target, goodId);
      if (goodAmount.Amount > 0) {
        candidates.Add(new PlannedCandidate(source, target, goodAmount, distance));
      }
    }
    candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
    var plannedAmount = 0;
    foreach (var candidate in candidates) {
      yield return (candidate.Source, candidate.Target, candidate.GoodAmount);
      plannedAmount += candidate.GoodAmount.Amount;
      if (plannedAmount >= requestedAmount) {
        break;
      }
    }
  }

  static IEnumerable<(Inventory source, Inventory target, GoodAmount goodAmount)> PlanTakingGoodCandidates(
      DistrictCenter districtCenter,
      Inventory source,
      GoodAmount availableGood,
      Predicate<Inventory> targetFilter) {
    if (availableGood.Amount <= 0 || !IsAvailableSource(source)) {
      yield break;
    }
    var sourceAccessible = source.GetEnabledComponent<Accessible>();
    if (!sourceAccessible) {
      yield break;
    }
    var candidates = new List<PlannedCandidate>();
    var districtInventoryRegistry = districtCenter.GetComponent<DistrictInventoryRegistry>();
    foreach (var target in districtInventoryRegistry.ActiveInventoriesWithCapacity(availableGood.GoodId)) {
      var targetAccessible = target.GetEnabledComponent<Accessible>();
      if (!targetAccessible
          || !IsAvailableTarget(target)
          || !targetFilter(target)
          || !IsTaking(target, availableGood.GoodId)
          || !sourceAccessible.FindRoadPath(targetAccessible, out var distance)) {
        continue;
      }
      var goodAmount = MaxTransferableAmount(source, target, availableGood);
      if (goodAmount.Amount > 0) {
        candidates.Add(new PlannedCandidate(source, target, goodAmount, distance));
      }
    }
    candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
    var plannedAmount = 0;
    var plannedTargets = 0;
    foreach (var candidate in candidates) {
      yield return (candidate.Source, candidate.Target, candidate.GoodAmount);
      plannedAmount += candidate.GoodAmount.Amount;
      plannedTargets++;
      if (plannedAmount >= availableGood.Amount || plannedTargets >= MaxTakeAwayTargetCandidates) {
        break;
      }
    }
  }

  static bool TryPlanTakingGood(
      DistrictCenter districtCenter,
      Inventory source,
      GoodAmount availableGood,
      Predicate<Inventory> targetFilter,
      out (Inventory source, Inventory target, GoodAmount goodAmount) order) {
    order = default;
    if (availableGood.Amount <= 0 || !IsAvailableSource(source)) {
      return false;
    }
    var sourceAccessible = source.GetEnabledComponent<Accessible>();
    if (!sourceAccessible) {
      return false;
    }
    var target = districtCenter.GetComponent<DistrictInventoryPicker>()
        .ClosestInventoryWithCapacity(
            sourceAccessible,
            new GoodAmount(availableGood.GoodId, 1),
            inventory => IsAvailableTarget(inventory) && targetFilter(inventory),
            out _);
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

  static bool IsAvailableSource(Inventory inventory) {
    return inventory && inventory.IsUnblocked;
  }

  static bool IsAvailableTarget(Inventory inventory) {
    return inventory && inventory.IsUnblocked;
  }

  static bool IsTaking(Inventory inventory, string goodId) {
    return inventory.HasUnreservedCapacity(goodId) && inventory.GetComponent<IInventoryValidator>().ValidInventory;
  }

  static IEnumerable<GoodAmount> GetUnreservedGoods(Inventory inventory) {
    var emptiable = inventory.GetComponent<Emptiable>();
    if (!emptiable || !emptiable.IsMarkedForEmptying) {
      return inventory.UnreservedTakeableStock();
    }
    return inventory.UnreservedStock();
  }

  static IEnumerable<GoodAmount> GetUnreservedOutputGoods(Inventory inventory) {
    if (inventory.OutputGoods.Count == 0) {
      return EmptyGoods();
    }
    return FilterGoods(inventory.UnreservedTakeableStock(), inventory.Gives);
  }

  static IEnumerable<GoodAmount> GetUnreservedUnwantedGoods(Inventory inventory) {
    return inventory.HasUnwantedStock ? inventory.UnreservedUnwantedStock() : EmptyGoods();
  }

  static IEnumerable<GoodAmount> FilterGoods(
      IEnumerable<GoodAmount> goodAmounts, Predicate<string> goodFilter) {
    foreach (var goodAmount in goodAmounts) {
      if (goodFilter(goodAmount.GoodId)) {
        yield return goodAmount;
      }
    }
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
      float weight,
      Inventory source,
      Inventory target,
      GoodAmount goodAmount) {
    return TransportOrderSnapshot.Queued(
        requesterId, haulCandidate, FormatBehaviorName(weightedBehavior.WorkplaceBehavior), weight,
        source, target, goodAmount);
  }

  static TransportOrderSnapshot WithRequest(
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventory source,
      Inventory target,
      GoodAmount goodAmount) {
    return WithRequest(
        haulCandidate, requesterId, weightedBehavior, weightedBehavior.Weight, source, target, goodAmount);
  }

  static bool TryCreateCoveredOrder(
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      float weight,
      Inventory target,
      string goodId,
      out TransportOrderSnapshot order) {
    var reservedCapacity = target.ReservedCapacity(goodId);
    if (reservedCapacity <= 0 || target.UnreservedCapacity(goodId) > 0) {
      order = default;
      return false;
    }
    order = TransportOrderSnapshot.Covered(
        requesterId, haulCandidate, FormatBehaviorName(weightedBehavior.WorkplaceBehavior), weight,
        target, new GoodAmount(goodId, reservedCapacity));
    return true;
  }

  static bool TryCreateCoveredOrder(
      HaulCandidate haulCandidate,
      Guid requesterId,
      WeightedBehavior weightedBehavior,
      Inventory target,
      string goodId,
      out TransportOrderSnapshot order) {
    return TryCreateCoveredOrder(
        haulCandidate, requesterId, weightedBehavior, weightedBehavior.Weight, target, goodId, out order);
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

  static float InputGoodWeight(Inventory target, string goodId, float fallbackWeight) {
    var limit = target.LimitedAmount(goodId);
    if (limit <= 0) {
      return fallbackWeight;
    }
    var fill = (float)target.AmountInStock(goodId) / limit;
    return Clamp01(1f - fill);
  }

  static float TakeAwayGoodWeight(Inventory source, string goodId, float fallbackWeight) {
    var limit = source.LimitedAmount(goodId);
    if (limit <= 0) {
      return source.AmountInStock(goodId) > 0 ? 1f : fallbackWeight;
    }
    var fill = (float)source.AmountInStock(goodId) / limit;
    return Clamp01(fill);
  }

  static float Clamp01(float value) {
    if (value < 0f) {
      return 0f;
    }
    return value > 1f ? 1f : value;
  }
}

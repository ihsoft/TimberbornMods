// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Timberborn.BuildingsNavigation;
using Timberborn.ConstructionSites;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.PrioritySystem;

namespace IgorZ.SmartHaulers.Dispatching;

static class ConstructionTransportOrderPlanner {
  const string BehaviorName = "BuildMaterials";

  public static bool TryPlan(
      DistrictCenter districtCenter,
      ConstructionJob constructionJob,
      IReadOnlyList<Accessible> builderHubAccessibles,
      Priority priority,
      out TransportOrderSnapshot order) {
    order = default;
    var constructionSite = constructionJob.GetComponent<ConstructionSite>();
    var constructionSiteAccessible = constructionJob.GetComponent<ConstructionSiteAccessible>()?.Accessible;
    if (!constructionSite || !constructionSite.IsOn || constructionSite.ReadyToBuild || !constructionSiteAccessible) {
      return false;
    }
    var remainingGoods = new SortedSet<GoodAmount>(new GoodAmountComparer());
    constructionSite.RemainingRequiredGoods(remainingGoods);
    foreach (var builderHubAccessible in builderHubAccessibles) {
      if (!TryPlanForBuilderHub(
          districtCenter, constructionSite, constructionSiteAccessible, builderHubAccessible, remainingGoods,
          priority, out order)) {
        continue;
      }
      remainingGoods.Clear();
      return true;
    }
    remainingGoods.Clear();
    return false;
  }

  static bool TryPlanForBuilderHub(
      DistrictCenter districtCenter,
      ConstructionSite constructionSite,
      Accessible constructionSiteAccessible,
      Accessible builderHubAccessible,
      SortedSet<GoodAmount> remainingGoods,
      Priority priority,
      out TransportOrderSnapshot order) {
    order = default;
    if (!builderHubAccessible.FindRoadToTerrainPath(constructionSiteAccessible, out var endOfRoad, out _)) {
      return false;
    }
    var districtInventoryPicker = districtCenter.GetComponent<DistrictInventoryPicker>();
    foreach (var remainingGood in remainingGoods) {
      var source = districtInventoryPicker.ClosestInventoryWithStock(
          endOfRoad, remainingGood.GoodId, builderHubAccessible);
      if (!source) {
        continue;
      }
      var goodAmount = MaxTransferableAmount(source, constructionSite.Inventory, remainingGood.GoodId);
      if (goodAmount.Amount <= 0) {
        continue;
      }
      order = CreateOrder(constructionSite, priority, source, constructionSite.Inventory, goodAmount);
      return true;
    }
    return false;
  }

  static TransportOrderSnapshot CreateOrder(
      ConstructionSite constructionSite, Priority priority, Inventory source, Inventory target, GoodAmount goodAmount) {
    var requesterId = constructionSite.GetComponent<EntityComponent>()?.EntityId ?? Guid.Empty;
    return TransportOrderSnapshot.Construction(
        requesterId, constructionSite, BehaviorName, PriorityWeight(priority), source, target, goodAmount);
  }

  static GoodAmount MaxTransferableAmount(Inventory source, Inventory target, string goodId) {
    var amount = Math.Min(source.UnreservedAmountInStock(goodId), target.UnreservedCapacity(goodId));
    return new GoodAmount(goodId, amount);
  }

  static float PriorityWeight(Priority priority) {
    return ((int)priority + 1f) / Enum.GetValues(typeof(Priority)).Length;
  }
}

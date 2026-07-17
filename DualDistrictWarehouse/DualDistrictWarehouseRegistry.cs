using System.Collections.Generic;
using Timberborn.Goods;
using Timberborn.ResourceCountingSystem;

namespace IgorZ.DualDistrictWarehouse;

sealed class DualDistrictWarehouseRegistry {
  readonly HashSet<DualDistrictWarehouse> _primaryHalves = [];

  internal static DualDistrictWarehouseRegistry Instance { get; private set; }

  public DualDistrictWarehouseRegistry() {
    Instance = this;
  }

  internal void Register(DualDistrictWarehouse warehouse) {
    _primaryHalves.Add(warehouse);
  }

  internal void Unregister(DualDistrictWarehouse warehouse) {
    _primaryHalves.Remove(warehouse);
  }

  internal ResourceCount Deduplicate(string goodId, ResourceCount resourceCount) {
    var duplicateStock = 0;
    var duplicateCapacity = 0;
    foreach (var warehouse in _primaryHalves) {
      var inventory = warehouse.Inventory;
      if (!inventory.Enabled || !inventory.Gives(goodId)) {
        continue;
      }

      duplicateStock += inventory.AmountInStock(goodId);
      if (inventory.PublicInput) {
        duplicateCapacity += inventory.LimitedAmount(goodId);
      }
    }

    if (duplicateStock == 0 && duplicateCapacity == 0) {
      return resourceCount;
    }

    var outputCapacity = resourceCount.TotalCapacity - resourceCount.InputOutputCapacity;
    return ResourceCount.Create(
        resourceCount.StockpiledStock - duplicateStock, resourceCount.BufferedOutputStock,
        resourceCount.InputOutputCapacity - duplicateCapacity, outputCapacity,
        resourceCount.CarriedToStockpilesStock, resourceCount.CarriedToProcessors,
        resourceCount.StockUnderProcessing, resourceCount.BufferedInput);
  }
}

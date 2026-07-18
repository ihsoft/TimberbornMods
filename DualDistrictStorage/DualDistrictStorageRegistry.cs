using System.Collections.Generic;
using Timberborn.Goods;
using Timberborn.ResourceCountingSystem;

namespace IgorZ.DualDistrictStorage;

sealed class DualDistrictStorageRegistry {
  readonly HashSet<DualDistrictStorage> _primaryHalves = [];

  internal static DualDistrictStorageRegistry Instance { get; private set; }

  public DualDistrictStorageRegistry() {
    Instance = this;
  }

  internal void Register(DualDistrictStorage storage) {
    _primaryHalves.Add(storage);
  }

  internal void Unregister(DualDistrictStorage storage) {
    _primaryHalves.Remove(storage);
  }

  internal ResourceCount Deduplicate(string goodId, ResourceCount resourceCount) {
    var duplicateStock = 0;
    var duplicateCapacity = 0;
    foreach (var storage in _primaryHalves) {
      var inventory = storage.Inventory;
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

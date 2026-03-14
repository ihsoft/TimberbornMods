// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.ConstructionSites;
using Timberborn.InventorySystem;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils;

/// <summary>Helper class to get various game components from a building.</summary>
public static class ComponentsAccessor {
  /// <summary>Gets the first non-construction site inventory of a building.</summary>
  /// <remarks>
  /// The building can have more than one inventory. This method finds one that is not used by the construction site.
  /// </remarks>
  public static Inventory GetGoodsInventory(BaseComponent building, bool throwIfNotFound = false) {
    var inventories = building.GetComponent<Inventories>();
    if (!inventories) {
      if (throwIfNotFound) {
        throw new InvalidOperationException("Inventories component not found");
      }
      return null;
    }
    var inventory = inventories.AllInventories
        .FirstOrDefault(x => x.ComponentName != ConstructionSiteInventoryInitializer.InventoryComponentName);
    if (!inventory && throwIfNotFound) {
      throw new InvalidOperationException("Inventory component not found");
    }
    return inventory;
  }
}

using System;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.ConstructionSites;
using Timberborn.InventorySystem;

namespace TimberDev.Tests;

static class ComponentsAccessorTests {
  public static void ReturnsFirstNonConstructionInventory() {
    var building = new BaseComponent();
    var inventories = new Inventories();
    var constructionInventory = new Inventory {
        ComponentName = ConstructionSiteInventoryInitializer.InventoryComponentName,
    };
    var goodsInventory = new Inventory {
        ComponentName = "Goods",
    };
    inventories.AllInventories.Add(constructionInventory);
    inventories.AllInventories.Add(goodsInventory);
    building.SetComponent(inventories);

    Assert.Equal(goodsInventory, ComponentsAccessor.GetGoodsInventory(building));
  }

  public static void HandlesMissingInventory() {
    var building = new BaseComponent();

    Assert.Equal(null, ComponentsAccessor.GetGoodsInventory(building));
    Assert.Throws<InvalidOperationException>(() => ComponentsAccessor.GetGoodsInventory(building, true));

    building.SetComponent(new Inventories());
    Assert.Equal(null, ComponentsAccessor.GetGoodsInventory(building));
    Assert.Throws<InvalidOperationException>(() => ComponentsAccessor.GetGoodsInventory(building, true));
  }
}

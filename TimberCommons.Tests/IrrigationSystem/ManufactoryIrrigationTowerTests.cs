using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.TimeSystem;
using Timberborn.Workshops;

namespace TimberCommons.Tests;

static class ManufactoryIrrigationTowerTests {
  public static void ScalesRecipeDurationFromCoverage() {
    var tower = CreateTower();

    tower.Coverage = 0.25f;
    tower.UpdateConsumptionRateForTest();

    var manufactory = tower.GetComponent<Manufactory>();
    Assert.Equal(32, manufactory.CurrentRecipe.CycleDurationInHours);
    Assert.Equal("128", tower.GetStats().progressBarMsg);

    tower.Coverage = 1f;
    tower.UpdateConsumptionRateForTest();

    Assert.Equal(8, manufactory.CurrentRecipe.CycleDurationInHours);
  }

  public static void ReportsNoTilesAtZeroCoverage() {
    var tower = CreateTower();

    tower.Coverage = 0f;
    tower.UpdateConsumptionRateForTest();

    var stats = tower.GetStats();
    Assert.Equal(8, tower.GetComponent<Manufactory>().CurrentRecipe.CycleDurationInHours);
    Assert.Equal("IgorZ.TimberCommons.WaterTower.NoTilesToIrrigate", stats.progressBarMsg);
  }

  static TestManufactoryIrrigationTower CreateTower() {
    var inventory = new Inventory();
    inventory.SetStock("Water", amount: 3, limitedAmount: 20);
    var recipe = new RecipeSpec {
        Id = "irrigate",
        CycleDurationInHours = 8,
        Ingredients = [new GoodAmountSpec { Id = "Water", Amount = 1 }],
    };
    var manufactory = new Manufactory {
        Inventory = inventory,
        CurrentRecipe = recipe,
        ProductionRecipes = [recipe],
    };
    var tower = new TestManufactoryIrrigationTower();
    tower.SetComponent(new BlockObject { IsFinished = true });
    tower.SetComponent(new BlockableObject());
    tower.SetComponent(manufactory);
    tower.SetComponent(new ManufactoryIrrigationTowerSpec { IrrigationRange = 5 });
    tower.InjectDependencies(new TestDayNightCycle(), new FakeLoc());
    tower.Awake();
    return tower;
  }

  sealed class TestManufactoryIrrigationTower : ManufactoryIrrigationTower {
    public void UpdateConsumptionRateForTest() {
      UpdateConsumptionRate();
    }
  }

  sealed class TestDayNightCycle : IDayNightCycle {
    public float FixedDeltaTimeInHours => 1;
  }
}

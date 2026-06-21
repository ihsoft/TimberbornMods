using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.GoodConsumingBuildingSystem;

namespace TimberCommons.Tests;

static class GoodConsumingIrrigationTowerTests {
  public static void ScalesConsumptionFromCoverage() {
    var tower = CreateTower(consumptionRate: 10);

    tower.Coverage = 0.25f;
    tower.UpdateConsumptionRateForTest();

    var building = tower.GetComponent<GoodConsumingBuilding>();
    Assert.Equal(2.5f, building._goodConsumingBuildingSpec.ConsumedGoods[0].GoodPerHour);
    Assert.Equal("60", tower.GetRate());

    tower.Coverage = 1f;
    tower.UpdateConsumptionRateForTest();

    Assert.Equal(10, building._goodConsumingBuildingSpec.ConsumedGoods[0].GoodPerHour);
  }

  public static void TogglesConsumptionFromCoverage() {
    var tower = CreateTower(consumptionRate: 10);
    var toggle = tower.GetComponent<GoodConsumingBuilding>().Toggle;

    tower.Coverage = 0f;
    tower.UpdateConsumptionRateForTest();

    Assert.Equal(1, toggle.PauseCalls);
    Assert.Equal(0, toggle.ResumeCalls);

    tower.Coverage = 0.5f;
    tower.UpdateConsumptionRateForTest();

    Assert.Equal(1, toggle.PauseCalls);
    Assert.Equal(1, toggle.ResumeCalls);
  }

  static TestGoodConsumingIrrigationTower CreateTower(float consumptionRate) {
    var tower = new TestGoodConsumingIrrigationTower();
    var buildingSpec = new GoodConsumingBuildingSpec {
        ConsumedGoods = [new ConsumedGoodSpec { GoodPerHour = consumptionRate }],
    };
    tower.SetComponent(new BlockObject { IsFinished = true });
    tower.SetComponent(new BlockableObject());
    tower.SetComponent(new GoodConsumingBuilding { _goodConsumingBuildingSpec = buildingSpec });
    tower.SetComponent(buildingSpec);
    tower.SetComponent(new GoodConsumingIrrigationTowerSpec { IrrigationRange = 5 });
    tower.InjectDependencies(new FakeLoc());
    tower.Awake();
    return tower;
  }

  sealed class TestGoodConsumingIrrigationTower : GoodConsumingIrrigationTower {
    public void UpdateConsumptionRateForTest() {
      UpdateConsumptionRate();
    }
  }
}

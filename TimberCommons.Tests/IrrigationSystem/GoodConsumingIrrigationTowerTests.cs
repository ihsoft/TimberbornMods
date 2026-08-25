using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.Buildings;
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

  public static void MoisturizesOnlyWhenConsumptionCanRun() {
    var tower = CreateTower(consumptionRate: 10);
    var building = tower.GetComponent<GoodConsumingBuilding>();

    Assert.True(tower.CanMoisturizeForTest());

    building.CanUse = false;
    Assert.False(tower.CanMoisturizeForTest());

    building.CanUse = true;
    building.ConsumptionPaused = true;
    Assert.False(tower.CanMoisturizeForTest());
  }

  public static void MultipliesFinishedBuildingEfficiencyProviders() {
    var unfinishedTower = CreateTower(consumptionRate: 10, isFinished: false);
    Assert.Equal(1, unfinishedTower.GetEfficiencyForTest());

    var finishedTower = CreateTower(
        consumptionRate: 10,
        efficiencyProviders: [new TestEfficiencyProvider(0.5f), new TestEfficiencyProvider(0.25f)]);

    Assert.Equal(0.125f, finishedTower.GetEfficiencyForTest());
  }

  static TestGoodConsumingIrrigationTower CreateTower(
      float consumptionRate, bool isFinished = true, IBuildingEfficiencyProvider[] efficiencyProviders = null) {
    var tower = new TestGoodConsumingIrrigationTower();
    var buildingSpec = new GoodConsumingBuildingSpec {
        ConsumedGoods = [new ConsumedGoodSpec { GoodPerHour = consumptionRate }],
    };
    tower.SetComponent(new BlockObject { IsFinished = isFinished });
    tower.SetComponent(new BlockableObject());
    tower.SetComponent(new GoodConsumingBuilding { _goodConsumingBuildingSpec = buildingSpec });
    tower.SetComponent(buildingSpec);
    tower.SetComponent(new GoodConsumingIrrigationTowerSpec { IrrigationRange = 5 });
    foreach (var efficiencyProvider in efficiencyProviders ?? []) {
      tower.AllComponents.Add(efficiencyProvider);
    }
    tower.InjectDependencies(new FakeLoc());
    tower.Awake();
    return tower;
  }

  sealed class TestGoodConsumingIrrigationTower : GoodConsumingIrrigationTower {
    public void UpdateConsumptionRateForTest() {
      UpdateConsumptionRate();
    }

    public bool CanMoisturizeForTest() {
      return CanMoisturize();
    }

    public float GetEfficiencyForTest() {
      return GetEfficiency();
    }
  }

  sealed class TestEfficiencyProvider(float efficiency) : IBuildingEfficiencyProvider {
    public float Efficiency { get; } = efficiency;
  }
}

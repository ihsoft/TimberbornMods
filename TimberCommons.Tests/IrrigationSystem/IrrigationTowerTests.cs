using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingRange;
using Timberborn.MapIndexSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace TimberCommons.Tests;

static class IrrigationTowerTests {
  public static void InitializesFlatFoundationCoverageFromLifecycle() {
    var tower = CreateTower();

    tower.InitializeEntity();

    Assert.Equal(8, tower.MaxCoveredTilesCount);
    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(8, tower.ReachableTiles.Count);
    Assert.Equal(1, tower.Coverage);
    Assert.Equal(1, tower.CurrentEfficiency);
    Assert.Equal(1, tower.EffectiveRange);
    Assert.Equal(2, tower.ConsumptionRateUpdates);
    Assert.Equal(0, tower.IrrigationStartedCalls);
  }

  public static void StartsMoistureOverrideOnceAndRemovesItOnExit() {
    var tower = CreateTower();
    tower.InitializeEntity();
    tower.OnEnterFinishedState();
    tower.CanMoisturizeValue = true;

    tower.Tick();
    tower.Tick();

    Assert.Equal(1, tower.SoilOverridesService.AddedMoistureOverrides.Count);
    Assert.Equal(8, tower.SoilOverridesService.ActiveMoistureOverrides.Count);
    Assert.Equal(1, tower.IrrigationStartedCalls);
    Assert.Equal(1, tower.EventBus.RegisteredObjects.Count);

    tower.OnExitFinishedState();

    Assert.Equal(1, tower.SoilOverridesService.RemovedMoistureOverrideIds.Count);
    Assert.Equal(-1, tower.SoilOverridesService.ActiveMoistureOverrideId);
    Assert.Equal(1, tower.IrrigationStoppedCalls);
    Assert.Equal(0, tower.EventBus.RegisteredObjects.Count);
    Assert.False(tower.Enabled);
  }

  public static void RemovesMoistureOverrideWhenBlocked() {
    var tower = CreateTower();
    var blockableObject = tower.GetComponent<BlockableObject>();
    tower.InitializeEntity();
    tower.OnEnterFinishedState();
    tower.CanMoisturizeValue = true;
    tower.Tick();

    blockableObject.Block();

    Assert.Equal(1, tower.SoilOverridesService.RemovedMoistureOverrideIds.Count);
    Assert.Equal(-1, tower.SoilOverridesService.ActiveMoistureOverrideId);
    Assert.Equal(1, tower.IrrigationStoppedCalls);
  }

  public static void RecalculatesCoverageWhenEfficiencyChanges() {
    var tower = CreateTower();
    tower.InitializeEntity();

    tower.Efficiency = 0;
    tower.Tick();

    Assert.Equal(0, tower.CurrentEfficiency);
    Assert.Equal(0, tower.EffectiveRange);
    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(0, tower.ReachableTiles.Count);
    Assert.Equal(0, tower.Coverage);
    Assert.Equal(3, tower.ConsumptionRateUpdates);
    Assert.Equal(0, tower.SoilOverridesService.AddedMoistureOverrides.Count);
  }

  static TestIrrigationTower CreateTower() {
    var positionedBlocks = new PositionedBlocks();
    positionedBlocks.AddBlock(new Vector3Int(5, 5, 0));

    var eventBus = new EventBus();
    var soilOverridesService = new SoilOverridesService();
    var tower = new TestIrrigationTower();
    tower.SetComponent(new BlockObject {
        Coordinates = new Vector3Int(5, 5, 0),
        IsFinished = true,
        Placement = new Placement { Coordinates = new Vector3Int(5, 5, 0) },
        PositionedBlocks = positionedBlocks,
    });
    tower.SetComponent(new BlockableObject());
    tower.EventBus = eventBus;
    tower.SoilOverridesService = soilOverridesService;
    tower.InjectDependencies(
        new TerrainMap(),
        new MapIndexService { TerrainSize = new Vector2Int(20, 20) },
        eventBus,
        soilOverridesService,
        new BuildingWithRangeUpdateService(),
        new TestTerrainService());
    tower.Awake();
    return tower;
  }

  sealed class TestIrrigationTower : IrrigationTower {
    public EventBus EventBus { get; set; }
    public SoilOverridesService SoilOverridesService { get; set; }
    public bool CanMoisturizeValue { get; set; }
    public float Efficiency { get; set; } = 1;
    public int ConsumptionRateUpdates { get; private set; }
    public int IrrigationStartedCalls { get; private set; }
    public int IrrigationStoppedCalls { get; private set; }

    protected override int IrrigationRange => 1;
    protected override bool IrrigateFromGroundTilesOnly => true;

    protected override bool CanMoisturize() {
      return CanMoisturizeValue;
    }

    protected override void IrrigationStarted() {
      IrrigationStartedCalls++;
    }

    protected override void IrrigationStopped() {
      IrrigationStoppedCalls++;
    }

    protected override void UpdateConsumptionRate() {
      ConsumptionRateUpdates++;
    }

    protected override float GetEfficiency() {
      return Efficiency;
    }
  }

  sealed class TestTerrainService : ITerrainService {
    public bool OnGround(Vector3Int coordinates) {
      return true;
    }
  }
}

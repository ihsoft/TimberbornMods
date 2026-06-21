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

  static TestIrrigationTower CreateTower() {
    var positionedBlocks = new PositionedBlocks();
    positionedBlocks.AddBlock(new Vector3Int(5, 5, 0));

    var tower = new TestIrrigationTower();
    tower.SetComponent(new BlockObject {
        Coordinates = new Vector3Int(5, 5, 0),
        IsFinished = true,
        Placement = new Placement { Coordinates = new Vector3Int(5, 5, 0) },
        PositionedBlocks = positionedBlocks,
    });
    tower.SetComponent(new BlockableObject());
    tower.InjectDependencies(
        new TerrainMap(),
        new MapIndexService { TerrainSize = new Vector2Int(20, 20) },
        new EventBus(),
        new SoilOverridesService(),
        new BuildingWithRangeUpdateService(),
        new TestTerrainService());
    tower.Awake();
    return tower;
  }

  sealed class TestIrrigationTower : IrrigationTower {
    public int ConsumptionRateUpdates { get; private set; }
    public int IrrigationStartedCalls { get; private set; }

    protected override int IrrigationRange => 1;
    protected override bool IrrigateFromGroundTilesOnly => true;

    protected override bool CanMoisturize() {
      return false;
    }

    protected override void IrrigationStarted() {
      IrrigationStartedCalls++;
    }

    protected override void IrrigationStopped() {
    }

    protected override void UpdateConsumptionRate() {
      ConsumptionRateUpdates++;
    }

    protected override float GetEfficiency() {
      return 1;
    }
  }

  sealed class TestTerrainService : ITerrainService {
    public bool OnGround(Vector3Int coordinates) {
      return true;
    }
  }
}

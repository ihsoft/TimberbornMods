using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingRange;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.Persistence;
using Timberborn.RangedEffectBuildingUI;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
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

  public static void SavesAndClaimsActiveMoistureOverride() {
    var sourceTower = CreateTower();
    sourceTower.InitializeEntity();
    sourceTower.OnEnterFinishedState();
    sourceTower.CanMoisturizeValue = true;
    sourceTower.Tick();

    var state = new EntityState();
    sourceTower.Save(state);
    var restoredTower = CreateTower();

    restoredTower.Load(state);

    Assert.Equal(1, restoredTower.SoilOverridesService.ClaimedMoistureOverrideIds.Count);
    Assert.Equal(1, restoredTower.SoilOverridesService.ClaimedMoistureOverrideIds[0]);
  }

  public static void RecalculatesCoverageWhenTerrainEligibilityChanges() {
    var tower = CreateTower();
    tower.InitializeEntity();
    tower.OnEnterFinishedState();

    tower.TerrainService.NonGroundTiles.Add(new Vector3Int(4, 5, 0));
    tower.TerrainMap.RemoveTerrain(new Vector3Int(4, 5, 0));

    Assert.Equal(7, tower.EligibleTiles.Count);
    Assert.Equal(7, tower.ReachableTiles.Count);
    Assert.Equal(0.875f, tower.Coverage);
    Assert.Equal(3, tower.ConsumptionRateUpdates);
  }

  public static void RecalculatesCoverageWhenSoilBarrierIsBuilt() {
    var tower = CreateTower();
    var barrierCoordinates = new Vector3Int(4, 5, 0);
    var barrier = CreateBarrierBlock(barrierCoordinates);
    tower.InitializeEntity();
    tower.OnEnterFinishedState();

    tower.SoilOverridesService.FullMoistureBarrierTiles.Add(barrierCoordinates);
    tower.OnEnteredFinishedStateEvent(new EnteredFinishedStateEvent(barrier));

    Assert.Equal(7, tower.EligibleTiles.Count);
    Assert.Equal(7, tower.ReachableTiles.Count);
    Assert.Equal(0.875f, tower.Coverage);
    Assert.Equal(3, tower.ConsumptionRateUpdates);
  }

  public static void RecalculatesCoverageWhenSoilBarrierIsDeleted() {
    var tower = CreateTower();
    var barrierCoordinates = new Vector3Int(4, 5, 0);
    var barrier = CreateBarrierBlock(barrierCoordinates);
    tower.SoilOverridesService.FullMoistureBarrierTiles.Add(barrierCoordinates);
    tower.InitializeEntity();

    tower.SoilOverridesService.FullMoistureBarrierTiles.Remove(barrierCoordinates);
    tower.OnEntityDeletedEvent(new EntityDeletedEvent(barrier));

    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(8, tower.ReachableTiles.Count);
    Assert.Equal(1, tower.Coverage);
    Assert.Equal(3, tower.ConsumptionRateUpdates);
  }

  public static void ReturnsPreviewRangeForUnfinishedBuilding() {
    var tower = CreateTower(isFinished: false);

    tower.OnPostPlacementChanged();

    var blocks = tower.GetBlocksInRange().ToHashSet();
    Assert.Equal(8, blocks.Count);
    Assert.True(blocks.Contains(new Vector3Int(4, 5, 0)));
    Assert.False(blocks.Contains(new Vector3Int(5, 5, 0)));
  }

  public static void ReturnsReachableOrEligibleRangeForFinishedBuilding() {
    var tower = CreateTower();
    tower.InitializeEntity();

    tower.Efficiency = 0;
    tower.Tick();

    Assert.Equal(0, tower.GetBlocksInRange().Count());

    tower.GetComponent<BlockableObject>().Block();

    Assert.Equal(8, tower.GetBlocksInRange().Count());
  }

  public static void PostPlacementChangeRecalculatesPreviewPositioning() {
    var tower = CreateTower(isFinished: false);
    var blockObject = tower.GetComponent<BlockObject>();
    tower.OnPostPlacementChanged();

    blockObject.PositionedBlocks.AddBlock(new Vector3Int(6, 5, 0));
    tower.OnPostPlacementChanged();

    var blocks = tower.GetBlocksInRange().ToHashSet();
    Assert.Equal(10, blocks.Count);
    Assert.False(blocks.Contains(new Vector3Int(5, 5, 0)));
    Assert.False(blocks.Contains(new Vector3Int(6, 5, 0)));
  }

  public static void PostInitializeEntityRecalculatesPreviewPositioning() {
    var tower = CreateTower(isFinished: false);
    var blockObject = tower.GetComponent<BlockObject>();
    tower.OnPostPlacementChanged();

    blockObject.PositionedBlocks.AddBlock(new Vector3Int(6, 5, 0));
    tower.PostInitializeEntity();

    var blocks = tower.GetBlocksInRange().ToHashSet();
    Assert.Equal(10, blocks.Count);
    Assert.False(blocks.Contains(new Vector3Int(5, 5, 0)));
    Assert.False(blocks.Contains(new Vector3Int(6, 5, 0)));
  }

  static TestIrrigationTower CreateTower(bool isFinished = true) {
    var positionedBlocks = new PositionedBlocks();
    positionedBlocks.AddBlock(new Vector3Int(5, 5, 0));

    var eventBus = new EventBus();
    var soilOverridesService = new SoilOverridesService();
    var terrainMap = new TerrainMap();
    var terrainService = new TestTerrainService();
    var tower = new TestIrrigationTower();
    tower.SetComponent(new BlockObject {
        Coordinates = new Vector3Int(5, 5, 0),
        IsFinished = isFinished,
        Placement = new Placement { Coordinates = new Vector3Int(5, 5, 0) },
        PositionedBlocks = positionedBlocks,
    });
    tower.SetComponent(new BlockableObject());
    tower.EventBus = eventBus;
    tower.SoilOverridesService = soilOverridesService;
    tower.TerrainMap = terrainMap;
    tower.TerrainService = terrainService;
    tower.InjectDependencies(
        terrainMap,
        new MapIndexService { TerrainSize = new Vector2Int(20, 20) },
        eventBus,
        soilOverridesService,
        new BuildingWithRangeUpdateService(),
        terrainService);
    tower.Awake();
    return tower;
  }

  static BlockObject CreateBarrierBlock(Vector3Int coordinates) {
    var barrier = new BlockObject {
        Coordinates = coordinates,
        IsFinished = true,
    };
    barrier.SetComponent(new SoilBarrierSpec { BlockFullMoisture = true });
    barrier.SetComponent(barrier);
    return barrier;
  }

  sealed class TestIrrigationTower : IrrigationTower {
    public EventBus EventBus { get; set; }
    public SoilOverridesService SoilOverridesService { get; set; }
    public TerrainMap TerrainMap { get; set; }
    public TestTerrainService TerrainService { get; set; }
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
    public HashSet<Vector3Int> NonGroundTiles { get; } = [];

    public bool OnGround(Vector3Int coordinates) {
      return !NonGroundTiles.Contains(coordinates);
    }
  }
}

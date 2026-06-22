using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingRange;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.RangedEffectBuildingUI;
using Timberborn.SelectionSystem;
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

  public static void StartsMoistureOverrideWithDistanceBasedDesertLevels() {
    var tower = CreateTower();
    tower.InitializeEntity();
    tower.OnEnterFinishedState();
    tower.CanMoisturizeValue = true;

    tower.Tick();

    var overrides = tower.SoilOverridesService.ActiveMoistureOverrides.ToDictionary(o => o.Coordinates);
    Assert.Equal(8, overrides.Count);
    Assert.Equal(1, overrides[new Vector3Int(4, 5, 0)].MoistureLevel);
    Assert.Equal(1.5f, overrides[new Vector3Int(4, 5, 0)].DesertLevel);
    Assert.Equal(2.5f - (float)System.Math.Sqrt(2), overrides[new Vector3Int(4, 4, 0)].DesertLevel);
  }

  public static void UnsubscribesFromRuntimeEventsOnExit() {
    var tower = CreateTower();
    var blockableObject = tower.GetComponent<BlockableObject>();
    var changedTile = new Vector3Int(4, 5, 0);
    tower.InitializeEntity();
    tower.OnEnterFinishedState();

    tower.OnExitFinishedState();

    tower.TerrainService.NonGroundTiles.Add(changedTile);
    tower.TerrainMap.RemoveTerrain(changedTile);
    tower.CanMoisturizeValue = true;
    blockableObject.Block();
    blockableObject.Unblock();

    Assert.Equal(2, tower.ConsumptionRateUpdates);
    Assert.Equal(0, tower.SoilOverridesService.AddedMoistureOverrides.Count);
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

  public static void RestoresSavedEfficiency() {
    var sourceTower = CreateTower(hasMechanicalNode: true);
    sourceTower.InitializeEntity();
    sourceTower.OnEnterFinishedState();
    sourceTower.CanMoisturizeValue = true;
    sourceTower.Tick();
    sourceTower.Tick();
    sourceTower.Tick();

    var state = new EntityState();
    sourceTower.Save(state);
    var restoredTower = CreateTower();

    restoredTower.Load(state);

    Assert.Equal(1, restoredTower.CurrentEfficiency);
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

  public static void RefreshesMoistureOverrideWhenCoverageChanges() {
    var tower = CreateTower();
    var changedTile = new Vector3Int(4, 5, 0);
    tower.InitializeEntity();
    tower.OnEnterFinishedState();
    tower.CanMoisturizeValue = true;
    tower.Tick();

    tower.TerrainService.NonGroundTiles.Add(changedTile);
    tower.TerrainMap.RemoveTerrain(changedTile);

    Assert.Equal(2, tower.SoilOverridesService.AddedMoistureOverrides.Count);
    Assert.Equal(1, tower.SoilOverridesService.RemovedMoistureOverrideIds.Count);
    Assert.Equal(2, tower.SoilOverridesService.ActiveMoistureOverrideId);
    Assert.Equal(7, tower.SoilOverridesService.ActiveMoistureOverrides.Count);
    Assert.False(tower.SoilOverridesService.ActiveMoistureOverrides.Any(o => o.Coordinates == changedTile));
    Assert.Equal(2, tower.IrrigationStartedCalls);
    Assert.Equal(1, tower.IrrigationStoppedCalls);
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

  public static void DelaysMechanicalEfficiencyChanges() {
    var tower = CreateTower(hasMechanicalNode: true);
    tower.InitializeEntity();

    Assert.Equal(0, tower.CurrentEfficiency);
    Assert.Equal(0, tower.ReachableTiles.Count);

    tower.Tick();
    tower.Tick();

    Assert.Equal(0, tower.CurrentEfficiency);
    Assert.Equal(0, tower.ReachableTiles.Count);

    tower.Tick();

    Assert.Equal(1, tower.CurrentEfficiency);
    Assert.Equal(8, tower.ReachableTiles.Count);

    tower.Efficiency = 0;
    tower.Tick();

    Assert.Equal(1, tower.CurrentEfficiency);
    Assert.Equal(8, tower.ReachableTiles.Count);

    tower.Tick();

    Assert.Equal(0, tower.CurrentEfficiency);
    Assert.Equal(0, tower.ReachableTiles.Count);
  }

  public static void RefreshesRangeHighlightOnlyWhileSelected() {
    var tower = CreateTower();
    var changedTile = new Vector3Int(4, 5, 0);
    tower.InitializeEntity();
    tower.OnEnterFinishedState();

    tower.TerrainService.NonGroundTiles.Add(changedTile);
    tower.TerrainMap.RemoveTerrain(changedTile);

    Assert.Equal(0, tower.BuildingWithRangeUpdateService.UnselectedEvents.Count);
    Assert.Equal(0, tower.BuildingWithRangeUpdateService.SelectedEvents.Count);

    tower.OnSelect();
    tower.TerrainService.NonGroundTiles.Remove(changedTile);
    tower.TerrainMap.AddTerrain(changedTile);

    Assert.Equal(1, tower.BuildingWithRangeUpdateService.UnselectedEvents.Count);
    Assert.Equal(1, tower.BuildingWithRangeUpdateService.SelectedEvents.Count);
    Assert.Equal(tower.SelectableObject, tower.BuildingWithRangeUpdateService.UnselectedEvents[0].SelectableObject);
    Assert.Equal(tower.SelectableObject, tower.BuildingWithRangeUpdateService.SelectedEvents[0].SelectableObject);

    tower.OnUnselect();
    tower.TerrainService.NonGroundTiles.Add(changedTile);
    tower.TerrainMap.RemoveTerrain(changedTile);

    Assert.Equal(1, tower.BuildingWithRangeUpdateService.UnselectedEvents.Count);
    Assert.Equal(1, tower.BuildingWithRangeUpdateService.SelectedEvents.Count);
  }

  public static void IgnoresIrrelevantTerrainEvents() {
    var tower = CreateTower();
    tower.InitializeEntity();
    tower.OnEnterFinishedState();

    tower.TerrainService.NonGroundTiles.Add(new Vector3Int(10, 10, 0));
    tower.TerrainMap.RemoveTerrain(new Vector3Int(10, 10, 0));
    tower.TerrainMap.AddTerrain(new Vector3Int(4, 5, 0));

    Assert.Equal(2, tower.ConsumptionRateUpdates);
    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(8, tower.ReachableTiles.Count);
  }

  public static void IgnoresIrrelevantSoilBarrierBuildEvents() {
    var tower = CreateTower();
    var barrierCoordinates = new Vector3Int(4, 5, 0);
    tower.InitializeEntity();
    tower.OnEnterFinishedState();

    tower.SoilOverridesService.GameLoaded = false;
    tower.SoilOverridesService.FullMoistureBarrierTiles.Add(barrierCoordinates);
    tower.OnEnteredFinishedStateEvent(new EnteredFinishedStateEvent(CreateBarrierBlock(barrierCoordinates)));

    tower.SoilOverridesService.GameLoaded = true;
    tower.OnEnteredFinishedStateEvent(new EnteredFinishedStateEvent(
        CreateBarrierBlock(barrierCoordinates, hasSpec: false)));
    tower.OnEnteredFinishedStateEvent(new EnteredFinishedStateEvent(CreateBarrierBlock(barrierCoordinates,
        blockFullMoisture: false)));

    Assert.Equal(2, tower.ConsumptionRateUpdates);
    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(8, tower.ReachableTiles.Count);
  }

  public static void IgnoresIrrelevantSoilBarrierDeleteEvents() {
    var tower = CreateTower();
    var barrierCoordinates = new Vector3Int(4, 5, 0);
    tower.SoilOverridesService.FullMoistureBarrierTiles.Add(barrierCoordinates);
    tower.InitializeEntity();

    tower.OnEntityDeletedEvent(new EntityDeletedEvent(CreateBarrierBlock(barrierCoordinates, isFinished: false)));
    tower.OnEntityDeletedEvent(new EntityDeletedEvent(CreateBarrierBlock(new Vector3Int(10, 10, 0))));
    tower.OnEntityDeletedEvent(new EntityDeletedEvent(CreateBarrierBlock(barrierCoordinates, blockFullMoisture: false)));

    Assert.Equal(2, tower.ConsumptionRateUpdates);
    Assert.Equal(7, tower.EligibleTiles.Count);
    Assert.Equal(7, tower.ReachableTiles.Count);
  }

  public static void CalculatesCoverageFromTwoByTwoFoundationBoundary() {
    var tower = CreateTower(foundationBlocks: [
        (new Vector3Int(5, 5, 0), MatterBelow.Ground),
        (new Vector3Int(6, 5, 0), MatterBelow.Ground),
        (new Vector3Int(5, 6, 0), MatterBelow.Ground),
        (new Vector3Int(6, 6, 0), MatterBelow.Ground),
    ]);

    tower.InitializeEntity();

    Assert.Equal(8, tower.MaxCoveredTilesCount);
    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(8, tower.ReachableTiles.Count);
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(5, 5, 0)));
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(6, 6, 0)));
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(4, 5, 0)));
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(7, 6, 0)));
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(4, 4, 0)));
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(7, 7, 0)));
  }

  public static void UsesAllFoundationTilesWhenGroundOnlyIsDisabled() {
    var tower = CreateTower(irrigateFromGroundTilesOnly: false, foundationBlocks: [
        (new Vector3Int(5, 5, 0), MatterBelow.Other),
    ]);

    tower.InitializeEntity();

    Assert.Equal(8, tower.MaxCoveredTilesCount);
    Assert.Equal(8, tower.EligibleTiles.Count);
    Assert.Equal(8, tower.ReachableTiles.Count);
  }

  public static void ClipsCoverageAtMapBounds() {
    var tower = CreateTower(foundationBlocks: [
        (new Vector3Int(0, 0, 0), MatterBelow.Ground),
    ]);

    tower.InitializeEntity();

    Assert.Equal(8, tower.MaxCoveredTilesCount);
    Assert.Equal(3, tower.EligibleTiles.Count);
    Assert.Equal(3, tower.ReachableTiles.Count);
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(0, 1, 0)));
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(1, 0, 0)));
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(1, 1, 0)));
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(-1, 0, 0)));
  }

  public static void TerrainObstaclesCutOffDisconnectedTiles() {
    var tower = CreateTower(irrigationRange: 2);
    for (var y = 3; y <= 7; y++) {
      tower.TerrainService.NonGroundTiles.Add(new Vector3Int(6, y, 0));
    }

    tower.InitializeEntity();

    Assert.Equal(20, tower.MaxCoveredTilesCount);
    Assert.Equal(12, tower.EligibleTiles.Count);
    Assert.Equal(12, tower.ReachableTiles.Count);
    Assert.Equal(0.6f, tower.Coverage);
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(6, 5, 0)));
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(7, 5, 0)));
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(4, 5, 0)));
  }

  public static void SoilBarriersCutOffDisconnectedTiles() {
    var tower = CreateTower(irrigationRange: 2);
    for (var y = 3; y <= 7; y++) {
      tower.SoilOverridesService.FullMoistureBarrierTiles.Add(new Vector3Int(6, y, 0));
    }

    tower.InitializeEntity();

    Assert.Equal(20, tower.MaxCoveredTilesCount);
    Assert.Equal(12, tower.EligibleTiles.Count);
    Assert.Equal(12, tower.ReachableTiles.Count);
    Assert.Equal(0.6f, tower.Coverage);
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(6, 5, 0)));
    Assert.False(tower.EligibleTiles.Contains(new Vector3Int(7, 5, 0)));
    Assert.True(tower.EligibleTiles.Contains(new Vector3Int(4, 5, 0)));
  }

  static TestIrrigationTower CreateTower(
      bool isFinished = true, bool hasMechanicalNode = false, bool irrigateFromGroundTilesOnly = true,
      int irrigationRange = 1, IEnumerable<(Vector3Int Coordinates, MatterBelow MatterBelow)> foundationBlocks = null) {
    var positionedBlocks = new PositionedBlocks();
    foreach (var (coordinates, matterBelow) in foundationBlocks ?? [(new Vector3Int(5, 5, 0), MatterBelow.Ground)]) {
      positionedBlocks.AddBlock(coordinates, matterBelow);
    }

    var eventBus = new EventBus();
    var soilOverridesService = new SoilOverridesService();
    var terrainMap = new TerrainMap();
    var terrainService = new TestTerrainService();
    var buildingWithRangeUpdateService = new BuildingWithRangeUpdateService();
    var selectableObject = new SelectableObject();
    var tower = new TestIrrigationTower();
    tower.SetComponent(new BlockObject {
        Coordinates = new Vector3Int(5, 5, 0),
        IsFinished = isFinished,
        Placement = new Placement { Coordinates = new Vector3Int(5, 5, 0) },
        PositionedBlocks = positionedBlocks,
    });
    tower.SetComponent(new BlockableObject());
    tower.SetComponent(selectableObject);
    if (hasMechanicalNode) {
      tower.SetComponent(new MechanicalNode());
    }
    tower.EventBus = eventBus;
    tower.SoilOverridesService = soilOverridesService;
    tower.TerrainMap = terrainMap;
    tower.TerrainService = terrainService;
    tower.BuildingWithRangeUpdateService = buildingWithRangeUpdateService;
    tower.SelectableObject = selectableObject;
    tower.IrrigateFromGroundTilesOnlyValue = irrigateFromGroundTilesOnly;
    tower.IrrigationRangeValue = irrigationRange;
    tower.InjectDependencies(
        terrainMap,
        new MapIndexService { TerrainSize = new Vector2Int(20, 20) },
        eventBus,
        soilOverridesService,
        buildingWithRangeUpdateService,
        terrainService);
    tower.Awake();
    return tower;
  }

  static BlockObject CreateBarrierBlock(
      Vector3Int coordinates, bool isFinished = true, bool hasSpec = true, bool blockFullMoisture = true) {
    var barrier = new BlockObject {
        Coordinates = coordinates,
        IsFinished = isFinished,
    };
    if (hasSpec) {
      barrier.SetComponent(new SoilBarrierSpec { BlockFullMoisture = blockFullMoisture });
    }
    barrier.SetComponent(barrier);
    return barrier;
  }

  sealed class TestIrrigationTower : IrrigationTower {
    public EventBus EventBus { get; set; }
    public SoilOverridesService SoilOverridesService { get; set; }
    public TerrainMap TerrainMap { get; set; }
    public TestTerrainService TerrainService { get; set; }
    public BuildingWithRangeUpdateService BuildingWithRangeUpdateService { get; set; }
    public SelectableObject SelectableObject { get; set; }
    public bool CanMoisturizeValue { get; set; }
    public bool IrrigateFromGroundTilesOnlyValue { get; set; } = true;
    public int IrrigationRangeValue { get; set; } = 1;
    public float Efficiency { get; set; } = 1;
    public int ConsumptionRateUpdates { get; private set; }
    public int IrrigationStartedCalls { get; private set; }
    public int IrrigationStoppedCalls { get; private set; }

    protected override int IrrigationRange => IrrigationRangeValue;
    protected override bool IrrigateFromGroundTilesOnly => IrrigateFromGroundTilesOnlyValue;

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

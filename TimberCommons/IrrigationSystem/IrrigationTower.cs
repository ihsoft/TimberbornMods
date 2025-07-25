﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using IgorZ.TimberDev.Utils;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingRange;
using Timberborn.BuildingsBlocking;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.MechanicalSystem;
using Timberborn.Persistence;
using Timberborn.RangedEffectBuildingUI;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using Timberborn.WorldPersistence;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>Base component for the irrigation towers.</summary>
/// <remarks>
/// The tower irrigates tiles (sets moisture level to a constant value) within a certain range. The ground level of the
/// eligible tiles must be the same as the tower itself. Only the "connected" tiles are eligible for irrigation.
/// Connected tiles must be immediately adjusted to each other on the left, right, top or bottom side. If the tile is
/// blocked for irrigation (for example, via a moisture blocker), then it is not eligible for irrigation. 
/// </remarks>
public abstract class IrrigationTower : TickableComponent, IBuildingWithRange, IFinishedStateListener,
                                        IPostPlacementChangeListener, IPausableComponent, ILateTickable,
                                        IPersistentEntity, ISelectionListener, IPostInitializableEntity {

  #region Unity conrolled fields
  // ReSharper disable InconsistentNaming
  // ReSharper disable RedundantDefaultMemberInitializer

  /// <summary>The maximum distance of irrigation from the building's boundary.</summary>
  [SerializeField]
  [Tooltip("The max distance from the building boundaries at which the tiles can get water.")]
  internal int _irrigationRange = 10;

  /// <summary>
  /// Indicates that only foundation tiles with "ground only" setting will be considered when searching for the eligible
  /// tiles.
  /// </summary>
  [SerializeField]
  [Tooltip("Indicates that only ground tiles will be used for irrigated tiles search.")]
  bool _irrigateFromGroundTilesOnly = true;

  // ReSharper restore InconsistentNaming
  // ReSharper restore RedundantDefaultMemberInitializer
  #endregion

  #region API
  // ReSharper disable MemberCanBeProtected.Global
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>Tells if the tower is initialized and ready to work.</summary>
  protected bool IsInitialized => MaxCoveredTilesCount > 0;

  /// <summary>The maximum irrigation range of the tower.</summary>
  /// <seealso cref="EligibleTiles"/>
  /// <seealso cref="EffectiveRange"/>
  public int IrrigationRange => _irrigationRange;

  /// <summary>The current irrigation range.</summary>
  /// <remarks>It can change based on the building efficiency.</remarks>
  /// <seealso cref="ReachableTiles"/>
  /// <seealso cref="IrrigationRange"/>
  public int EffectiveRange => Mathf.RoundToInt(_irrigationRange * CurrentEfficiency);

  /// <summary>The tiles that can get water.</summary>
  /// <remarks>These tiles are in the effective range and can be reached from the tower.</remarks>
  /// <see cref="EffectiveRange"/>
  public HashSet<Vector3Int> ReachableTiles { get; private set; } = [];

  /// <summary>The tiles that can be irrigated at the 100% efficiency.</summary>
  /// <see cref="IrrigationRange"/>
  public HashSet<Vector3Int> EligibleTiles { get; private set; } = [];

  /// <summary>
  /// The maximum number of tiles which this component could irrigate on a flat surface if there were no irrigation
  /// obstacles.
  /// </summary>
  public int MaxCoveredTilesCount { get; private set; }

  /// <summary>The percentile of the tiles that are reachable to the maximum coverage.</summary>
  /// <remarks>
  /// This value is used to scale the building consumption. All goods consumption in the buildings should be configured
  /// for the maximum coverage.</remarks>
  /// <seealso cref="ReachableTiles"/>
  /// <seealso cref="MaxCoveredTilesCount"/>
  /// <seealso cref="UpdateConsumptionRate"/>
  public float Coverage { get; private set; }

  /// <summary>Tells if the building is being irrigating the tiles in range.</summary>
  /// <seealso cref="ReachableTiles"/>
  public bool IsIrrigating => _moistureOverrideIndex != -1;

  /// <summary>The last calculated efficiency modifier.</summary>
  public float CurrentEfficiency { get; private set; }

  /// <summary>Shortcut to the building's block object.</summary>
  protected BlockObject BlockObject { get; private set; }

  /// <summary>The tower is always a blockable building.</summary>
  protected BlockableBuilding BlockableBuilding { get; private set; }

  // ReSharper restore MemberCanBeProtected.Global
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region IBuildingWithRange implementation

  /// <inheritdoc />
  public IEnumerable<Vector3Int> GetBlocksInRange() {
    if (!BlockObject.IsFinished) {
      return GetTiles(range: _irrigationRange, skipChecks: false).eligible;
    }
    return BlockableBuilding.IsUnblocked ? ReachableTiles : EligibleTiles;
  }

  /// <inheritdoc />
  public IEnumerable<BaseComponent> GetObjectsInRange() {
    return [];
  }

  /// <inheritdoc />
  public string RangeName => typeof(IrrigationTower).FullName;

  #endregion

  #region IPostTransformChangeListener implementation
  
  /// <inheritdoc />
  public void OnSelect() {
    _towerSelected = true;
  }

  /// <inheritdoc />
  public void OnUnselect() {
    _towerSelected = false;
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public virtual void OnEnterFinishedState() {
    _terrainMap.TerrainAdded += OnTerrainChanged;
    _terrainMap.TerrainRemoved += OnTerrainChanged;
    BlockableBuilding.BuildingBlocked += OnBlockedStateChanged;
    BlockableBuilding.BuildingUnblocked += OnBlockedStateChanged;
    _eventBus.Register(this);
    enabled = true;
  }

  /// <inheritdoc/>
  public virtual void OnExitFinishedState() {
    StopMoisturizing();
    _eventBus.Unregister(this);
    enabled = false;
  }

  #endregion

  #region IPostPlacementChangeListener implementation

  /// <inheritdoc/>
  public void OnPostPlacementChanged() {
    UpdateBuildingPositioning(); // Recalculate the coverage during preview mode.
  }

  #endregion

  #region IPostInitializableEntity implementation

  /// <inheritdoc/>
  public void PostInitializeEntity() {
    UpdateBuildingPositioning();
  }

  #endregion

  #region TickableComponent implementation

  /// <inheritdoc/>
  public override void StartTickable() {
    base.StartTickable();
    Initialize();

    _needsPower = GetComponentFast<MechanicalNode>();
    if (_needsPower) {
      _skipTicks = 1;
    }
    UpdateCoverage();
    UpdateState();
  }
  bool _needsPower;

  /// <inheritdoc/>
  public override void Tick() {
    UpdateState();
    --_skipTicks;
  }
  int _skipTicks;

  #endregion

  #region Overridable methods

  /// <summary>Indicates if the tower has everything to moisturize the tiles.</summary>
  protected abstract bool CanMoisturize();

  /// <summary>Notifies that the irrigation started.</summary>
  /// <seealso cref="ReachableTiles"/>
  protected abstract void IrrigationStarted();

  /// <summary>Notifies that the irrigation process has stopped.</summary>
  protected abstract void IrrigationStopped();

  /// <summary>Called when the tower consumption rate needs to be recalculated.</summary>
  /// <seealso cref="EligibleTiles"/>
  /// <seealso cref="ReachableTiles"/>
  protected abstract void UpdateConsumptionRate();
 
  /// <summary>Returns the current building efficiency modifier.</summary>
  /// <remarks>Values less <c>1.0</c> reduce the effective range.</remarks>
  /// <seealso cref="EffectiveRange"/>
  protected abstract float GetEfficiency();

  /// <summary>Initializes the component right before the first tick.</summary>
  protected virtual void Initialize() {
    if (!MaxCoverageByRadius.TryGetValue(_foundationSize, out var coverages)) {
      coverages = new Dictionary<int, int>();
      MaxCoverageByRadius.Add(_foundationSize, coverages);
    }
    if (!coverages.TryGetValue(_irrigationRange, out var coverage)) {
      coverage = GetTiles(range: _irrigationRange, skipChecks: true).eligible.Count;
      coverages.Add(_irrigationRange, coverage);
      HostedDebugLog.Fine(this, "Calculated max coverage: size={0}, range={1}, coverage={2}",
                          _foundationSize, _irrigationRange, coverage);
    }
    MaxCoveredTilesCount = coverage;
  }
  static readonly Dictionary<Vector2Int, Dictionary<int, int>> MaxCoverageByRadius = new();

  #endregion

  #region Implemenation

  /// <summary>This is a delta to be added to any distance value to account the building's boundaries.</summary>
  /// <remarks>
  /// All the tiles are considered in terms of "a range from the boundary", not from the building's center. The bigger
  /// the boundary (building's size), the greater is this adjuster. Note that it doesn't handle well the case of the
  /// boundary that is not a perfect square. 
  /// </remarks>
  float _radiusAdjuster;

  /// <summary>The moisture override, registered in the direct moisture system component.</summary>
  /// <remarks>Value <c>-1</c> means the moisture override wasn't setup.</remarks>
  /// <seealso cref="StartMoisturizing"/>
  /// <seealso cref="StopMoisturizing"/>
  int _moistureOverrideIndex = -1;

  /// <summary>The Z level at which this building will irrigate the tiles.</summary>
  /// <remarks>The irrigation won't work above or below.</remarks>
  /// <seealso cref="UpdateBuildingPositioning"/>
  int _baseZ;

  /// <summary>The size of the building. The range is counted from the boundaries.</summary>
  Vector2Int _foundationSize;

  /// <summary>The building approximated center to use when determining the range.</summary>
  /// <remarks>The coordinates may not be integer. It depends on the actual building size.</remarks>
  /// <seealso cref="UpdateBuildingPositioning"/>
  Vector2 _buildingCenter;

  /// <summary>Cached positioned blocks to start searching for the eligible tiles.</summary>
  List<Vector3Int> _startingTiles;

  /// <summary>All tiles that had non-baseZ height during the last eligible tiles refresh.</summary>
  /// <remarks>React on the terrain changes to these tiles only.</remarks>
  /// <seealso cref="_baseZ"/>
  /// <seealso cref="GetTiles"/>
  HashSet<Vector3Int> _irrigationObstacles = [];

  /// <summary>All tiles that had an irrigation barrier set during the last eligible tiles refresh.</summary>
  /// <remarks>When an entity is deleted, only react to it if it was a barrier.</remarks>
  /// <seealso cref="GetTiles"/>
  HashSet<Vector3Int> _irrigationBarriers = [];

  /// <summary>Indicates if any tower is selected. This enables the highlighting range update.</summary>
  static bool _towerSelected;

  TerrainMap _terrainMap;
  MapIndexService _mapIndexService;
  EventBus _eventBus;
  SoilOverridesService _soilOverridesService;
  BuildingWithRangeUpdateService _buildingWithRangeUpdateService;
  ITerrainService _terrainService;

  HashSet<Vector3Int> _foundationTilesIndexes = [];

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(TerrainMap terrainMap,
                                 MapIndexService mapIndexService, EventBus eventBus,
                                 SoilOverridesService soilOverridesService,
                                 BuildingWithRangeUpdateService buildingWithRangeUpdateService,
                                 ITerrainService terrainService) {
    _terrainMap = terrainMap;
    _mapIndexService = mapIndexService;
    _eventBus = eventBus;
    _soilOverridesService = soilOverridesService;
    _buildingWithRangeUpdateService = buildingWithRangeUpdateService;
    _terrainService = terrainService;
  }

  /// <summary>Awake is called when the script instance is being loaded.</summary>
  protected virtual void Awake() {
    BlockObject = GetComponentFast<BlockObject>();
    BlockableBuilding = GetComponentFast<BlockableBuilding>();
    enabled = false;
  }

  /// <summary>Updates the eligible tiles and moisture system.</summary>
  /// <remarks>If case of there are changes in the irrigating tiles, the moisturizing will be stopped.</remarks>
  void UpdateState() {
    if (!IsInitialized || _skipTicks > 0) {
      return;  // Skip state checks until the component is ready.
    }
    var newEfficiency = GetEfficiency();
    if (Mathf.Abs(CurrentEfficiency - newEfficiency) >= float.Epsilon) {
      // Power can fluctuate within one tick when switching from generators to batteries.
      if (!_needsPower || _lastTickChangedEfficiency) {
        _lastTickChangedEfficiency = false;
        HostedDebugLog.Fine(this, "Efficiency changed: {0} -> {1}", CurrentEfficiency, newEfficiency);
        CurrentEfficiency = newEfficiency;
        UpdateCoverage();
      } else {
        _lastTickChangedEfficiency = true;
      }
    } else {
      _lastTickChangedEfficiency = false;
    }
    if (BlockableBuilding.IsUnblocked && CanMoisturize()) {
      StartMoisturizing();
    } else {
      StopMoisturizing();
    }
  }
  bool _lastTickChangedEfficiency;

  /// <summary>Starts logic on the irrigated tiles.</summary>
  void StartMoisturizing() {
    if (_moistureOverrideIndex != -1) {
      return;
    }
    var overrides = ReachableTiles
        .Select(tile => new MoistureOverride(tile, 1.0f, CalculateDesertLevel(tile.XY(), EffectiveRange)));
    _moistureOverrideIndex = _soilOverridesService.AddMoistureOverride(overrides);
    IrrigationStarted();
  }

  /// <summary>Stops any logic on the irrigated tiles.</summary>
  void StopMoisturizing() {
    if (_moistureOverrideIndex == -1) {
      return;
    }
    _soilOverridesService.RemoveMoistureOverride(_moistureOverrideIndex);
    _moistureOverrideIndex = -1;
    IrrigationStopped();
  }

  /// <summary>Updates the building size and position to its current state.</summary>
  void UpdateBuildingPositioning() {
    var foundation = BlockObject.PositionedBlocks.GetFoundationCoordinates();
    var minX = int.MaxValue;
    var maxX = int.MinValue;
    var minY = int.MaxValue;
    var maxY = int.MinValue;
    foreach (var pos in foundation) {
      minX = Math.Min(minX, pos.x);
      maxX = Math.Max(maxX, pos.x);
      minY = Math.Min(minY, pos.y);
      maxY = Math.Max(maxY, pos.y);
    }
    var dx = maxX - minX;
    var dy = maxY - minY;
    _foundationSize = new Vector2Int(dx + 1, dy + 1);
    _buildingCenter = new Vector2(minX + dx / 2.0f, minY + dy / 2.0f);
    _radiusAdjuster = Math.Max(dx + 1, dy + 1) / 2.0f;
    _baseZ = BlockObject.Placement.Coordinates.z;

    if (_irrigateFromGroundTilesOnly) {
      _startingTiles = BlockObject.PositionedBlocks.GetAllBlocks()
          .Where(b => b.MatterBelow == MatterBelow.Ground && b.Coordinates.z == _baseZ)
          .Select(b => b.Coordinates)
          .ToList();
    } else {
      _startingTiles = BlockObject.PositionedBlocks.GetFoundationCoordinates().Where(c => c.z == _baseZ).ToList();
    }
    _foundationTilesIndexes = BlockObject.PositionedBlocks.GetFoundationCoordinates()
        .Where(coordinates => coordinates.z == _baseZ)
        .ToHashSet();
  }

  /// <summary>Calculates the tile's "moisture look" based on its distance from the tower.</summary>
  float CalculateDesertLevel(Vector2Int tile, float range) {
    var maxIrrigatedDistance = range + _radiusAdjuster;
    var distance = new Vector2(_buildingCenter.x - tile.x, _buildingCenter.y - tile.y).magnitude;
    return maxIrrigatedDistance - distance + 1f;  // The farthest tile gets moisture level 1.0f.
  }

  /// <summary>Gets square distance of the tile form the building's center.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  float GetSqrtDistance(Vector2Int tile) {
    return (tile.x - _buildingCenter.x) * (tile.x - _buildingCenter.x)
        + (tile.y - _buildingCenter.y) * (tile.y - _buildingCenter.y);
  }

  /// <summary>Returns all the tiles in the irrigated range.</summary>
  (HashSet<Vector3Int> eligible, HashSet<Vector3Int> obstacles, HashSet<Vector3Int> barriers) GetTiles(
    float range, bool skipChecks) {
    var tilesToVisit = new List<Vector3Int>(Mathf.RoundToInt(range * range));
    var visitedTiles = new HashSet<Vector3Int>();
    var result = new HashSet<Vector3Int>();
    var obstacles = new HashSet<Vector3Int>();
    var barriers = new HashSet<Vector3Int>();
    var sqrRadius = (range + _radiusAdjuster) * (range + _radiusAdjuster);
    var mapWidth = _mapIndexService.TerrainSize.x;
    var mapHeight = _mapIndexService.TerrainSize.y;

    tilesToVisit.AddRange(_startingTiles);
    for (var i = 0; i < tilesToVisit.Count; i++) {
      var tile = tilesToVisit[i];
      if (!visitedTiles.Add(tile)) {
        continue; // Already checked, skip it.
      }
      if (GetSqrtDistance(tile.XY()) > sqrRadius) {
        continue;
      }
      var coordinates = new Vector3Int(tile.x, tile.y, _baseZ);
      if (!skipChecks) {
        if (tile.x < 0 || tile.x >= mapWidth || tile.y < 0 || tile.y >= mapHeight) {
          continue;
        }
        if (!_terrainService.OnGround(coordinates)) {
          obstacles.Add(coordinates);
          continue;
        }
        if (_soilOverridesService.IsFullMoistureBarrierAt(coordinates)) {
          barriers.Add(coordinates);
          continue;
        }
      }
      if (!_foundationTilesIndexes.Contains(tile)) {
        result.Add(coordinates);
      }
      var up = new Vector3Int(tile.x, tile.y - 1, _baseZ);
      tilesToVisit.Add(up);
      var down = new Vector3Int(tile.x, tile.y + 1, _baseZ);
      tilesToVisit.Add(down);
      var left = new Vector3Int(tile.x - 1, tile.y, _baseZ);
      tilesToVisit.Add(left);
      var right = new Vector3Int(tile.x + 1, tile.y, _baseZ);
      tilesToVisit.Add(right);
    }

    return (result, obstacles, barriers);
  }

  /// <summary>Rebuilds tiles coverage of the tower.</summary>
  void UpdateCoverage() {
    (EligibleTiles, _irrigationObstacles, _irrigationBarriers) = GetTiles(range: _irrigationRange, skipChecks: false);
    var newIrrigatedTiles = _irrigationRange == EffectiveRange
        ? EligibleTiles
        : GetTiles(range: EffectiveRange, skipChecks: false).eligible;
    ReachableTiles = newIrrigatedTiles;
    Coverage = (float)newIrrigatedTiles.Count / MaxCoveredTilesCount;
    HostedDebugLog.Fine(this, "Covered tiles updated: eligible={0}, irrigated={1}, utilization={2}, efficiency={3}",
                        EligibleTiles.Count, ReachableTiles.Count, Coverage, CurrentEfficiency);
    UpdateConsumptionRate();
    MaybeRefreshRangeHighlight();

    if (IsIrrigating) {
      StopMoisturizing();
      StartMoisturizing();
    }
  }

  /// <summary>Refreshes the range highlights if the tower is selected.</summary>
  void MaybeRefreshRangeHighlight() {
    if (!_towerSelected) {
      return;
    }
    var thisSelectable = GetComponentFast<SelectableObject>();
    _buildingWithRangeUpdateService.OnSelectableObjectUnselected(new SelectableObjectUnselectedEvent(thisSelectable));
    _buildingWithRangeUpdateService.OnSelectableObjectSelected(new SelectableObjectSelectedEvent(thisSelectable));
  }

  #endregion

  #region Terrain and buildings change callbacks

  /// <summary>Monitors building new soil barriers within the range.</summary>
  [OnEvent]
  public void OnEnteredFinishedStateEvent(EnteredFinishedStateEvent e) {
    if (!_soilOverridesService.GameLoaded) {
      return;  // Don't run logic during the game load.
    }
    var blockObject = e.BlockObject;
    var checkCoordinates = new Vector3Int(blockObject.Coordinates.x, blockObject.Coordinates.y, _baseZ);
    if (!EligibleTiles.Contains(checkCoordinates)) {
      return;
    }
    var barrier = blockObject.GetComponentFast<SoilBarrierSpec>();
    if (!barrier || !barrier.BlockFullMoisture) {
      return;
    }
    HostedDebugLog.Fine(this, "Soil barrier construction completed in the affected area: {0}", e.BlockObject);
    UpdateCoverage();
  }

  /// <summary>Monitors soil barriers removal within the range.</summary>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent e) {
    var blockObject = e.Entity.GetComponentFast<BlockObject>();
    if (!blockObject || !blockObject.IsFinished || !_irrigationBarriers.Contains(blockObject.Coordinates)) {
      return;
    }
    var barrier = e.Entity.GetComponentFast<SoilBarrierSpec>();
    if (!barrier || !barrier.BlockFullMoisture) {
      return;
    }
    HostedDebugLog.Fine(this, "Soil barrier deleted in affected area: {0}", blockObject);
    UpdateCoverage();
  }

  void OnTerrainChanged(object sender, Vector3Int coordinates) {
    var checkCoordinates = new Vector3Int(coordinates.x, coordinates.y, _baseZ);
    var wasEligible = EligibleTiles.Contains(checkCoordinates);
    var wasIneligible = _irrigationObstacles.Contains(checkCoordinates);
    if (!wasEligible && !wasIneligible) {
      return;  // The tile is not in the range, skip it.
    }
    var nowEligible = _terrainService.OnGround(checkCoordinates);
    if (wasEligible && nowEligible || wasIneligible && !nowEligible) {
      return;  // Eligible state didn't change.
    }
    HostedDebugLog.Fine(this, "Terrain changed at tile: coords={0}, wasEligible={1}, nowEligible={2}",
                        coordinates, wasEligible, nowEligible);
    UpdateCoverage();
  }

  #endregion

  #region Component callbacks

  /// <summary>Updates towers state based on the blocking state.</summary>
  void OnBlockedStateChanged(object sender, EventArgs e) {
    UpdateState();
  }

  #endregion

  #region IPersistentEntity implemenatation

  static readonly ComponentKey IrrigationTowerKey = new(typeof(IrrigationTower).FullName);
  static readonly PropertyKey<float> CurrentEfficiencyKey = new("CurrentEfficiency");
  static readonly PropertyKey<int> MoistureOverrideIndexKey = new("MoistureOverrideIndex");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    if (!IsIrrigating) {
      return;
    }
    var component = entitySaver.GetComponent(IrrigationTowerKey);
    component.Set(CurrentEfficiencyKey, CurrentEfficiency);
    component.Set(MoistureOverrideIndexKey, _moistureOverrideIndex);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(IrrigationTowerKey, out var component)) {
      return;
    }
    CurrentEfficiency = component.Get(CurrentEfficiencyKey);
    _moistureOverrideIndex = component.Get(MoistureOverrideIndexKey);
    if (_moistureOverrideIndex != -1) {
      _soilOverridesService.ClaimMoistureOverrideIndex(_moistureOverrideIndex);
    }
  }

  #endregion
}
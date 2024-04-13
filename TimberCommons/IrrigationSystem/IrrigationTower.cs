// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingRange;
using Timberborn.BuildingsBlocking;
using Timberborn.BuildingsUI;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.Persistence;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

/// <summary>Base component for the irrigation towers.</summary>
/// <remarks>
/// The tower irrigates tiles (sets moisture level to a constant value) within a certain range. The ground level of the
/// eligible tiles must be the same as the tower itself. Only the "connected" tiles are eligible for irrigation.
/// Connected tiles must be immediately adjusted to each other on left, right, top or bottom side. If the tile is
/// blocked for irrigation (e.g. via a moisture blocker), then it's not eligible for irrigation. 
/// </remarks>
public abstract class IrrigationTower : TickableComponent, IBuildingWithRange, IFinishedStateListener,
                                        IPostTransformChangeListener, IPausableComponent, ILateTickable,
                                        IPersistentEntity, ISelectionListener {

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

  /// <summary>The maximum irrigation range of the tower.</summary>
  /// <seealso cref="EligibleTiles"/>
  /// <seealso cref="EffectiveRange"/>
  public int IrrigationRange => _irrigationRange;

  /// <summary>The current irrigation range.</summary>
  /// <remarks>It can change based on the building efficiency.</remarks>
  /// <seealso cref="ReachableTiles"/>
  /// <seealso cref="IrrigationRange"/>
  public int EffectiveRange => Mathf.RoundToInt(_irrigationRange * _currentEfficiency);

  /// <summary>The tiles that can get water.</summary>
  /// <remarks>These tiles are in the effective range and can be reached from the tower.</remarks>
  /// <see cref="EffectiveRange"/>
  public HashSet<Vector2Int> ReachableTiles { get; private set; } = new();

  /// <summary>The tiles that can be irrigated at the 100% efficiency.</summary>
  /// <see cref="IrrigationRange"/>
  public HashSet<Vector2Int> EligibleTiles { get; private set; } = new();

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

  /// <summary>Shortcut to the building's block object.</summary>
  protected BlockObject BlockObject { get; private set; }

  /// <summary>Tower is always a blockable building.</summary>
  protected BlockableBuilding BlockableBuilding { get; private set; }

  // ReSharper restore MemberCanBeProtected.Global
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region IBuildingWithRange implementation

  /// <inheritdoc />
  public IEnumerable<Vector3Int> GetBlocksInRange() {
    if (!BlockObject.Finished) {
      return GetTiles(range: _irrigationRange, skipChecks: false).eligible
          .Select(v => new Vector3Int(v.x, v.y, _baseZ));
    }
    return BlockableBuilding.IsUnblocked
        ? ReachableTiles.Select(v => new Vector3Int(v.x, v.y, _baseZ))
        : EligibleTiles.Select(v => new Vector3Int(v.x, v.y, _baseZ));
  }

  /// <inheritdoc />
  public IEnumerable<BaseComponent> GetObjectsInRange() {
    return Enumerable.Empty<BaseComponent>();
  }

  /// <inheritdoc />
  public IEnumerable<string> RangeNames() {
    yield return typeof(IrrigationTower).FullName;
  }

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
    _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
    BlockableBuilding.BuildingBlocked += OnBlockedStateChanged;
    BlockableBuilding.BuildingUnblocked += OnBlockedStateChanged;
    _eventBus.Register(this);
  }

  /// <inheritdoc/>
  public virtual void OnExitFinishedState() {
    StopMoisturizing();
    _terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
    BlockableBuilding.BuildingBlocked -= OnBlockedStateChanged;
    BlockableBuilding.BuildingUnblocked -= OnBlockedStateChanged;
    _eventBus.Unregister(this);
  }

  #endregion

  #region IPostTransformChangeListener implementation

  /// <inheritdoc/>
  public void OnPostTransformChanged() {
    UpdateBuildingPositioning(); // Recalculate the coverage during preview mode.
  }

  #endregion

  #region TickableComponent implementation

  /// <inheritdoc/>
  public override void StartTickable() {
    base.StartTickable();
    Initialize();
    UpdateState();
  }

  /// <inheritdoc/>
  public override void Tick() => UpdateState();

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
    UpdateBuildingPositioning();
    if (!MaxCoverageByRadius.TryGetValue(_foundationSize, out var coverages)) {
      coverages = new Dictionary<int, int>();
      MaxCoverageByRadius.Add(_foundationSize, coverages);
    }
    if (!coverages.TryGetValue(_irrigationRange, out var coverage)) {
      coverage = GetTiles(range: _irrigationRange, skipChecks: true).eligible.Count;
      coverages.Add(_irrigationRange, coverage);
      DebugEx.Fine(
          "Calculated max coverage: size={0}, range={1}, coverage={2}", _foundationSize, _irrigationRange, coverage);
    }
    MaxCoveredTilesCount = coverage;
  }
  static readonly Dictionary<Vector2Int, Dictionary<int, int>> MaxCoverageByRadius = new();

  #endregion

  #region Implemenation

  ITerrainService _terrainService;
  SoilBarrierMap _soilBarrierMap;
  MapIndexService _mapIndexService;
  EventBus _eventBus;
  DirectSoilMoistureSystemAccessor _directSoilMoistureSystemAccessor;
  HashSet<int> _foundationTilesIndexes = new();

  /// <summary>This is a delta to be added to any distance value to account the building's boundaries.</summary>
  /// <remarks>
  /// All the tiles are considered in terms of "a range from the boundary", not from the building's center. The bigger
  /// the boundary (building's size), the greater is this adjuster. Not that it doesn't handle well the case of the
  /// boundary that is not a perfect square. 
  /// </remarks>
  float _radiusAdjuster;

  /// <summary>The moisture override, registered in the direct moisture system component.</summary>
  /// <remarks>Value <c>-1</c> means the moisture override was not setup.</remarks>
  /// <seealso cref="StartMoisturizing"/>
  /// <seealso cref="StopMoisturizing"/>
  int _moistureOverrideIndex = -1;

  /// <summary>If set to <c>true</c>, then the irrigated tiles set will be updated on the next tick.</summary>
  /// <remarks>
  /// Set this flag each time when something that may affect irrigate has happen. The update is is not cheap, so don't
  /// trigger it on every tick.
  /// </remarks>
  /// <seealso cref="UpdateState"/>
  bool _needMoistureSystemUpdate = true;

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

  /// <summary>The last calculated efficiency modifier.</summary>
  float _currentEfficiency = 1.0f;

  /// <summary>The loaded efficiency from the save state.</summary>
  /// <remarks>It's only saved for the active towers. Used to restore the initial irrigated state.</remarks>
  float _savedEfficiency = -1;

  /// <summary>Cached positioned blocks to start searching for the eligible tiles.</summary>
  List<Vector2Int> _startingTiles;

  /// <summary>All tiles that had non-baseZ height during the last eligible tiles refresh.</summary>
  /// <remarks>React on the terrain changes to these tiles only.</remarks>
  /// <seealso cref="_baseZ"/>
  /// <seealso cref="GetTiles"/>
  HashSet<int> _irrigationObstacles = new();

  /// <summary>All tiles that had an irrigation barrier set during the last eligible tiles refresh.</summary>
  /// <remarks>When en entity is deleted, only react to it if was a barrier.</remarks>
  /// <seealso cref="GetTiles"/>
  HashSet<int> _irrigationBarriers = new();

  /// <summary>Indicates if any tower is selected. This enables the highlighting range update.</summary>
  static bool _towerSelected;

  /// <summary>
  /// Indicates how many ticks need to be skipped before actually updating the state due to the efficiency change.
  /// </summary>
  /// <remarks>Value of <c>-1</c> means this setting should be disregarded.</remarks>
  int _delayEfficiencyChangeUpdateTicks = -1;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ITerrainService terrainService, SoilBarrierMap soilBarrierMap,
                                 MapIndexService mapIndexService, EventBus eventBus,
                                 DirectSoilMoistureSystemAccessor directSoilMoistureSystemAccessor) {
    _terrainService = terrainService;
    _soilBarrierMap = soilBarrierMap;
    _mapIndexService = mapIndexService;
    _eventBus = eventBus;
    _directSoilMoistureSystemAccessor = directSoilMoistureSystemAccessor;
  }

  /// <summary>Awake is called when the script instance is being loaded.</summary>
  protected virtual void Awake() {
    BlockObject = GetComponentFast<BlockObject>();
    BlockableBuilding = GetComponentFast<BlockableBuilding>();
  }

  /// <summary>Updates the eligible tiles and moisture system.</summary>
  /// <remarks>If case of there are changes in the irrigating tiles, the moisturising will be stopped.</remarks>
  /// <seealso cref="_needMoistureSystemUpdate"/>
  void UpdateState() {
    // Efficiency affects the irrigated radius. It can fluctuate during one tick, so delay the decision.
    var newEfficiency = _savedEfficiency >= 0 ? _savedEfficiency : GetEfficiency();
    if (Mathf.Abs(_currentEfficiency - newEfficiency) >= float.Epsilon) {
      if (_delayEfficiencyChangeUpdateTicks < 0) {  // Skip, if there is another request pending.
        _delayEfficiencyChangeUpdateTicks = 2;  // The code right below will expend 1 tick.
      }
    } else {
      _delayEfficiencyChangeUpdateTicks = -1;  // The efficiency is back to normal, no update needed.
    }

    // Check for the delayed efficiency change.
    if (_delayEfficiencyChangeUpdateTicks > 0) {
      if (--_delayEfficiencyChangeUpdateTicks == 0) {
        _needMoistureSystemUpdate = true;
      }
    }

    // Sync the state.
    if (_needMoistureSystemUpdate) {
      _needMoistureSystemUpdate = false;
      _currentEfficiency = newEfficiency;
      _delayEfficiencyChangeUpdateTicks = -1; 
      (EligibleTiles, _irrigationObstacles, _irrigationBarriers) = GetTiles(range: _irrigationRange, skipChecks: false);
      var newIrrigatedTiles = _irrigationRange == EffectiveRange
          ? EligibleTiles
          : GetTiles(range: EffectiveRange, skipChecks: false).eligible;
      StopMoisturizing();
      ReachableTiles = newIrrigatedTiles;
      Coverage = (float)newIrrigatedTiles.Count / MaxCoveredTilesCount;
      HostedDebugLog.Fine(this, "Covered tiles updated: eligible={0}, irrigated={1}, utilization={2}, efficiency={3}",
                          EligibleTiles.Count, ReachableTiles.Count, Coverage, _currentEfficiency);
      UpdateConsumptionRate();
      if (_towerSelected) {
        GetComponentFast<BuildingWithRangeUpdater>().OnPostTransformChanged();
      }
    }

    if (_savedEfficiency >= 0) {
      _savedEfficiency = -1;
      StartMoisturizing();  // This only happens on initialization of a formerly active tower. 
    } else {
      if (BlockableBuilding.IsUnblocked && CanMoisturize()) {
        StartMoisturizing();
      } else {
        StopMoisturizing();
      }
    }
  }

  /// <summary>Starts logic on the irrigated tiles.</summary>
  void StartMoisturizing() {
    if (_moistureOverrideIndex != -1) {
      return;
    }
    _moistureOverrideIndex = _directSoilMoistureSystemAccessor.AddMoistureOverride(
        ReachableTiles, 1.0f, tile => CalculateDesertLevel(tile, EffectiveRange));
    IrrigationStarted();
  }

  /// <summary>Stops any logic on the irrigated tiles.</summary>
  void StopMoisturizing() {
    if (_moistureOverrideIndex == -1) {
      return;
    }
    _directSoilMoistureSystemAccessor.RemoveMoistureOverride(_moistureOverrideIndex);
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
          .Where(x => x.MatterBelow == MatterBelow.Ground)
          .Select(x => x.Coordinates.XY())
          .ToList();
    } else {
      _startingTiles = BlockObject.PositionedBlocks.GetFoundationCoordinates()
          .Select(x => x.XY())
          .ToList();
    }
    _foundationTilesIndexes = BlockObject.PositionedBlocks.GetFoundationCoordinates()
        .Select(c => _mapIndexService.CoordinatesToIndex(c.XY()))
        .ToHashSet();
  }

  /// <summary>Calculates the tile's "moisture look" based on its distance from the tower.</summary>
  float CalculateDesertLevel(Vector2Int tile, float range) {
    var maxIrrigatedDistance = range + _radiusAdjuster;
    var distance = new Vector2(_buildingCenter.x - tile.x, _buildingCenter.y - tile.y).magnitude;
    return maxIrrigatedDistance - distance + 1f;  // The farthest tile gets moisture level 1.0f.
  }

  /// <summary>Get's square distance of the tile form the building's center.</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  float GetSqrtDistance(Vector2Int tile) {
    return (tile.x - _buildingCenter.x) * (tile.x - _buildingCenter.x)
        + (tile.y - _buildingCenter.y) * (tile.y - _buildingCenter.y);
  }

  /// <summary>Returns all the tiles in the irrigated range.</summary>
  (HashSet<Vector2Int> eligible, HashSet<int> obstacles, HashSet<int> barriers) GetTiles(
      float range, bool skipChecks) {
    var tilesToVisit = new List<Vector2Int>(Mathf.RoundToInt(range * range));
    var visitedTiles = new HashSet<int>();
    var result = new HashSet<Vector2Int>();
    var obstacles = new HashSet<int>();
    var barriers = new HashSet<int>();
    var sqrRadius = (range + _radiusAdjuster) * (range + _radiusAdjuster);
    var mapWidth = _mapIndexService.MapSize.x;
    var mapHeight = _mapIndexService.MapSize.y;

    _startingTiles.ForEach(tile => tilesToVisit.Add(tile));
    for (var i = 0; i < tilesToVisit.Count; i++) {
      var tile = tilesToVisit[i];
      var index = _mapIndexService.CoordinatesToIndex(tile);
      if (!visitedTiles.Add(index)) {
        continue; // Already checked, skip it.
      }
      if (GetSqrtDistance(tile) > sqrRadius) {
        continue;
      }
      if (!skipChecks) {
        if (tile.x < 0 || tile.x >= mapWidth || tile.y < 0 || tile.y >= mapHeight) {
          continue;
        }
        if (_terrainService.UnsafeCellHeight(index) != _baseZ) {
          obstacles.Add(index);
          continue;
        }
        if (_soilBarrierMap.FullMoistureBarriers[index]) {
          barriers.Add(index);
          continue;
        }
      }
      if (!_foundationTilesIndexes.Contains(index)) {
        result.Add(tile);
      }
      var up = new Vector2Int(tile.x, tile.y - 1);
      tilesToVisit.Add(up);
      var down = new Vector2Int(tile.x, tile.y + 1);
      tilesToVisit.Add(down);
      var left = new Vector2Int(tile.x - 1, tile.y);
      tilesToVisit.Add(left);
      var right = new Vector2Int(tile.x + 1, tile.y);
      tilesToVisit.Add(right);
    }

    return (result, obstacles, barriers);
  }

  #endregion

  #region Terrain and buildings change callbacks

  /// <summary>Triggers the tiles update if something is built within the range.</summary>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent e) {
    var coordinates = e.Constructible.GetComponentFast<BlockObject>().Coordinates.XY();
    var index = _mapIndexService.CoordinatesToIndex(coordinates);
    if (EligibleTiles.Contains(coordinates) && _soilBarrierMap.FullMoistureBarriers[index]) {
      HostedDebugLog.Fine(this, "Full moisture barrier added: coords={0}, barrier={1}", coordinates, e.Constructible);
      _needMoistureSystemUpdate = true;
    }
  }

  /// <summary>Triggers the tiles update if something has been destroyed within the range.</summary>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent e) {
    var constructable = e.Entity.GetComponentFast<Constructible>();
    if (constructable == null || !constructable.IsFinished) {
      return; // The preview objects cannot affect irrigation.
    }
    var coordinates = e.Entity.GetComponentFast<BlockObject>().Coordinates.XY();
    var index = _mapIndexService.CoordinatesToIndex(coordinates);
    if (_irrigationBarriers.Contains(index) && !_soilBarrierMap.FullMoistureBarriers[index]) {
      HostedDebugLog.Fine(this, "Full moisture barrier removed: at={0}, barrier={1}", coordinates, e.Entity);
      _needMoistureSystemUpdate = true;
    }
  }

  /// <summary>The terrain changes must trigger the irrigation coverage re-calculation.</summary>
  void OnTerrainHeightChanged(object sender, TerrainHeightChangedEventArgs e) {
    var index = _mapIndexService.CoordinatesToIndex(e.Coordinates);
    if (e.NewHeight == _baseZ && _irrigationObstacles.Contains(index)
        || e.OldHeight == _baseZ && ReachableTiles.Contains(e.Coordinates)) {
      HostedDebugLog.Fine(this, "Terrain height change detected: coords={0}, from={1}, to={2}",
                          e.Coordinates, e.OldHeight, e.NewHeight);
      _needMoistureSystemUpdate = true;
    }
  }

  #endregion

  #region Component callbacks

  /// <summary>Updates range selection since blocked towers can show different tiles.</summary>
  void OnBlockedStateChanged(object sender, EventArgs e) {
    GetComponentFast<BuildingWithRangeUpdater>().OnPostTransformChanged();
    UpdateState();
  }

  #endregion

  #region IPersistentEntity implemenatation

  static readonly ComponentKey IrrigationTowerKey = new(typeof(IrrigationTower).FullName);
  static readonly PropertyKey<float> CurrentEfficiencyKey = new("CurrentEfficiency");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    if (!IsIrrigating) {
      return;
    }
    var component = entitySaver.GetComponent(IrrigationTowerKey);
    component.Set(CurrentEfficiencyKey, _currentEfficiency);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.HasComponent(IrrigationTowerKey)) {
      return;
    }
    var component = entityLoader.GetComponent(IrrigationTowerKey);
    if (component.Has(CurrentEfficiencyKey)) {
      _savedEfficiency = component.Get(CurrentEfficiencyKey);
    }
  }

  #endregion
}

}

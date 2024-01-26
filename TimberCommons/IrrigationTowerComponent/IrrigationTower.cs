// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingRange;
using Timberborn.BuildingsUI;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
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
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public abstract class IrrigationTower : TickableComponent, IBuildingWithRange, IFinishedStateListener,
                                        IPostTransformChangeListener {
  #region Unity conrolled fields

  /// <summary>The maximum distance of irrigation from the building's boundary.</summary>
  [SerializeField]
  // ReSharper disable once InconsistentNaming
  internal int _irrigationRange = 10;

  /// <summary>The moisture level of the tiles in range.</summary>
  [SerializeField]
  // ReSharper disable once InconsistentNaming
  internal float _moistureLevel = 1.0f;

  /// <summary>The optional name to use to group irrigation ranges in preview.</summary>
  [SerializeField]
  // ReSharper disable once InconsistentNaming
  internal string _rangeName;

  #endregion

  #region API

  /// <summary>The number of tiles that are getting water.</summary>
  /// <remarks>This is how many tiles are in the effective range and can be reached from the tower.</remarks>
  /// <see cref="EffectiveRange"/>
  public int IrrigatedTilesCount { get; private set; }

  /// <summary>The number of tiles that can be irrigated at the 100% efficiency.</summary>
  /// <see cref="_irrigationRange"/>
  public int EligibleTilesCount { get; private set; }

  /// <summary>
  /// The maximum number of tiles which this component could irrigate on a flat surface if there were no irrigation
  /// obstacles.
  /// </summary>
  public int MaxCoveredTilesCount { get; private set; }

  /// <summary>Tells if the building is being irrigating the tiles in range.</summary>
  /// <seealso cref="IrrigatedTilesCount"/>
  public bool IsIrrigating => _moistureOverrideIndex != -1;

  /// <summary>The current irrigation range.</summary>
  /// <remarks>It can change based on the building efficiency providers reports.</remarks>
  /// <seealso cref="IrrigatedTilesCount"/>
  public int EffectiveRange => Mathf.RoundToInt(_irrigationRange * _currentEfficiency);

  /// <summary>The block object of this tower.</summary>
  protected BlockObject BlockObject { get; private set; }

  #endregion

  #region IBuildingWithRange implementation

  /// <inheritdoc />
  public IEnumerable<Vector3Int> GetBlocksInRange() {
    return GetTiles(range: EffectiveRange, skipChecks: false).Select(v => new Vector3Int(v.x, v.y, _baseZ));
  }

  /// <inheritdoc />
  public IEnumerable<BaseComponent> GetObjectsInRange() {
    return Enumerable.Empty<BaseComponent>();
  }

  /// <inheritdoc />
  public IEnumerable<string> RangeNames() {
    yield return _rangeName ?? typeof(IrrigationTower).FullName;
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    _terrainService.TerrainHeightChanged += OnTerrainHeightChanged;
    _eventBus.Register(this);
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    StopMoisturizing();
    _terrainService.TerrainHeightChanged -= OnTerrainHeightChanged;
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
  static readonly Dictionary<Vector2Int, Dictionary<int, int>> MaxCoverageByRadius = new();

  /// <inheritdoc/>
  public override void Tick() => UpdateState();

  #endregion

  #region Overridable methods

  /// <summary>Indicates if the tower has everything to moisturizing the tiles.</summary>
  protected abstract bool CanMoisturize();

  /// <summary>Notifies that the irrigation started on the provided tiles.</summary>
  /// <param name="tiles">All the affected tiles.</param>
  /// <seealso cref="IrrigatedTilesCount"/>
  protected abstract void IrrigationStarted(IEnumerable<Vector2Int> tiles);

  /// <summary>Notifies that the irrigation process has stopped.</summary>
  protected abstract void IrrigationStopped();

  /// <summary>Called when the tower consumption rate needs to be recalculated.</summary>
  /// <seealso cref="EligibleTilesCount"/>
  /// <seealso cref="IrrigatedTilesCount"/>
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
      coverage = GetTiles(range: _irrigationRange, skipChecks: true).Count;
      coverages.Add(_irrigationRange, coverage);
      DebugEx.Fine(
          "Calculated max coverage: size={0}, range={1}, coverage={2}", _foundationSize, _irrigationRange, coverage);
    }
    MaxCoveredTilesCount = coverage;
  }

  #endregion

  #region Implemenation

  ITerrainService _terrainService;
  SoilBarrierMap _soilBarrierMap;
  MapIndexService _mapIndexService;
  EventBus _eventBus;
  DirectSoilMoistureSystemAccessor _directSoilMoistureSystemAccessor;

  float _adjustedMaxSqrRadius;
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

  protected virtual void Awake() {
    BlockObject = GetComponentFast<BlockObject>();
  }

  /// <summary>Updates the eligible tiles and moisture system.</summary>
  /// <remarks>If case of there are changes in the irrigating tiles, the moisturising will be stopped.</remarks>
  /// <seealso cref="_needMoistureSystemUpdate"/>
  void UpdateState() {
    // Efficiency affects the irrigated radius.
    var newEfficiency = GetEfficiency();
    if (Mathf.Abs(_currentEfficiency - newEfficiency) > float.Epsilon) {
      HostedDebugLog.Fine(this, "Efficiency changed: {0} => {1}", _currentEfficiency, newEfficiency);
      _currentEfficiency = newEfficiency;
      _needMoistureSystemUpdate = true;
    }

    // Sync the state.
    if (_needMoistureSystemUpdate) {
      _needMoistureSystemUpdate = false;
      StopMoisturizing();
      IrrigatedTilesCount = GetTiles(range: EffectiveRange, skipChecks: false).Count;
      EligibleTilesCount = GetTiles(range: _irrigationRange, skipChecks: false).Count;
      HostedDebugLog.Fine(this, "Tiles updated: eligible={0}, irrigated={1}, efficiency={2}",
                          EligibleTilesCount, IrrigatedTilesCount, _currentEfficiency);
      UpdateConsumptionRate();
      GetComponentFast<BuildingWithRangeUpdater>().OnPostTransformChanged();
    }

    if (CanMoisturize()) {
      StartMoisturizing();
    } else {
      StopMoisturizing();
    }
  }

  /// <summary>Starts logic on the irrigated tiles.</summary>
  void StartMoisturizing() {
    if (_moistureOverrideIndex != -1) {
      return;
    }
    var irrigatedTiles = GetTiles(range: EffectiveRange, skipChecks: false);
    _moistureOverrideIndex = _directSoilMoistureSystemAccessor.AddMoistureOverride(irrigatedTiles, _moistureLevel);
    IrrigationStarted(irrigatedTiles);
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
    _adjustedMaxSqrRadius = (_irrigationRange + _radiusAdjuster) * (_irrigationRange + _radiusAdjuster);
    _baseZ = BlockObject.Placement.Coordinates.z;
  }

  /// <summary>Tells if the provided tile is within the tower's max range.</summary>
  /// <seealso cref="_irrigationRange"/>
  bool IsTileInRange(Vector2Int tile) {
    var sqrDist = (tile.x - _buildingCenter.x) * (tile.x - _buildingCenter.x)
        + (tile.y - _buildingCenter.y) * (tile.y - _buildingCenter.y);
    return sqrDist <= _adjustedMaxSqrRadius;
  }

  /// <summary>Returns all the tiles in the irrigated range.</summary>
  HashSet<Vector2Int> GetTiles(float range, bool skipChecks) {
    var tilesToVisit = new Queue<Vector2Int>();
    var visitedTiles = new HashSet<Vector2Int>();
    var result = new List<Vector2Int>();
    var sqrRadius = (range + _radiusAdjuster) * (range + _radiusAdjuster);

    tilesToVisit.Enqueue(BlockObject.Placement.Coordinates.XY());
    while (!tilesToVisit.IsEmpty()) {
      var tile = tilesToVisit.Dequeue();
      if (!skipChecks) {
        if (tile.x < 0 || tile.x >= _mapIndexService.MapSize.x || tile.y < 0 || tile.y >= _mapIndexService.MapSize.y) {
          continue; // Reject the out of map tiles.
        }
      }
      if (!visitedTiles.Add(tile)) {
        continue; // Already checked, skip it.
      }
      var index = _mapIndexService.CoordinatesToIndex(tile);
      var sqrDist = (tile.x - _buildingCenter.x) * (tile.x - _buildingCenter.x)
          + (tile.y - _buildingCenter.y) * (tile.y - _buildingCenter.y);
      if (sqrDist > sqrRadius
          || !skipChecks && _terrainService.UnsafeCellHeight(index) != _baseZ
          || !skipChecks && _soilBarrierMap.FullMoistureBarriers[index]) {
        continue; // Not eligible.
      }
      result.Add(tile);
      var up = new Vector2Int(tile.x, tile.y - 1);
      tilesToVisit.Enqueue(up);
      var down = new Vector2Int(tile.x, tile.y + 1);
      tilesToVisit.Enqueue(down);
      var left = new Vector2Int(tile.x - 1, tile.y);
      tilesToVisit.Enqueue(left);
      var right = new Vector2Int(tile.x + 1, tile.y);
      tilesToVisit.Enqueue(right);
    }

    // The building itself is not being irrigated. Remove it from the tiles.
    var final = result.ToHashSet();
    var foundation = BlockObject.PositionedBlocks.GetFoundationCoordinates().Select(c => c.XY()).ToHashSet();
    final.RemoveWhere(x => foundation.Contains(x));
    return final;
  }

  #endregion

  #region Terrain and buildings change callbacks

  /// <summary>Triggers the tiles update if something is built within the range.</summary>
  /// <seealso cref="EligibleTilesCount"/>
  /// <seealso cref="IrrigatedTilesCount"/>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent e) {
    if (IsTileInRange(e.Constructible.GetComponentFast<BlockObject>().Coordinates.XY())) {
      _needMoistureSystemUpdate = true;
    }
  }

  /// <summary>Triggers the tiles update if something has been destroyed within the range.</summary>
  /// <seealso cref="EligibleTilesCount"/>
  /// <seealso cref="IrrigatedTilesCount"/>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent e) {
    var constructable = e.Entity.GetComponentFast<Constructible>();
    if (constructable == null || !constructable.IsFinished) {
      return; // The preview objects cannot affect irrigation.
    }
    if (IsTileInRange(e.Entity.GetComponentFast<BlockObject>().Coordinates.XY())) {
      _needMoistureSystemUpdate = true;
    }
  }

  /// <summary>The terrain changes must trigger the irrigation coverage re-calculation.</summary>
  void OnTerrainHeightChanged(object sender, TerrainHeightChangedEventArgs e) {
    if (IsTileInRange(e.Coordinates)) {
      _needMoistureSystemUpdate = true;
    }
  }

  #endregion
}
}

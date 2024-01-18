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
using Timberborn.Buildings;
using Timberborn.BuildingsUI;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.MapIndexSystem;
using Timberborn.PrefabSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.Reflections;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationTowerComponent {
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
class IrrigationTower : TickableComponent, IBuildingWithRange, IFinishedStateListener, IPostTransformChangeListener {
  #region Unity conrolled fields

  /// <summary>The maximum distance of irrigation from the building's boundary.</summary>
  [SerializeField]
  // ReSharper disable once InconsistentNaming
  internal int _irrigationRange = 10;

  /// <summary>The moisture level of the tiles in range.</summary>
  [SerializeField]
  // ReSharper disable once InconsistentNaming
  internal float _moistureLevel = 1.0f;

  #endregion

  #region API

  /// <summary>The tiles that are eligible to get water.</summary>
  /// <remarks>The actual number of irrigated tiles can be less if the building efficiency is not 100%.</remarks>
  /// <seealso cref="IsIrrigating"/>
  /// <seealso cref="EffectiveRange"/>
  public HashSet<Vector2Int> EligibleTiles { get; private set; }

  /// <summary>Tells if the building is being irrigating the tiles in range.</summary>
  /// <seealso cref="EligibleTiles"/>
  public bool IsIrrigating => _overrideIndex != -1;

  /// <summary>The number of tiles which this component could irrigate if there were no irrigation obstacles.</summary>
  /// <remarks>It's the base to determine the <see cref="IrrigationCoverage"/>.</remarks>
  public int IrrigationMaxCoverage { get; private set; }

  /// <summary>The percentage of the actually irrigated tiles compared to the total number of tiles in range.</summary>
  /// <remarks>Due to the terrain layout, not all of the tiles in range may be eligible to the irrigation.</remarks>
  /// <seealso cref="IsIrrigating"/>
  public float IrrigationCoverage { get; private set; }

  /// <summary>The current irrigation range.</summary>
  /// <remarks>It can change based on the building efficiency providers reports.</remarks>
  public int EffectiveRange => Mathf.RoundToInt(_irrigationRange * _currentEfficiency);

  #endregion

  #region IBuildingWithRange implementation

  /// <inheritdoc />
  public IEnumerable<Vector3Int> GetBlocksInRange() {
    return EligibleTiles != null
        ? EligibleTiles.Select(v => new Vector3Int(v.x, v.y, _baseZ)).ToArray()
        : GetTiles(useEffectiveRange: false, skipChecks: false).Select(v => new Vector3Int(v.x, v.y, _baseZ));
  }

  /// <inheritdoc />
  public IEnumerable<BaseComponent> GetObjectsInRange() {
    return Enumerable.Empty<BaseComponent>();
  }

  /// <inheritdoc />
  public IEnumerable<string> RangeNames() {
    yield return _prefab.PrefabName;
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
    if (_blockObject.Preview) {
      enabled = false;
      return;
    }
    UpdateBuildingPositioning();
    IrrigationMaxCoverage = GetTiles(useEffectiveRange: false, skipChecks: true).Count;
    _tilesLayoutChanged = true;
    UpdateState();
  }

  /// <inheritdoc/>
  public override void Tick() {
    UpdateState();
  }

  #endregion

  #region Implemenation

  static readonly ReflectedField<GoodConsumingBuilding, float> GoodPerHourField = new(
      "_goodPerHour", throwOnFailure: true);

  ITerrainService _terrainService;
  SoilBarrierMap _soilBarrierMap;
  MapIndexService _mapIndexService;
  EventBus _eventBus;
  DirectSoilMoistureSystemAccessor _directSoilMoistureSystemAccessor;

  BlockObject _blockObject;
  Prefab _prefab;
  GoodConsumingBuilding _goodConsumingBuilding;
  readonly List<IBuildingEfficiencyProvider> _efficiencyProviders = new();

  float _adjustedMaxSqrRadius;
  float _radiusAdjuster;

  /// <summary>The override, registered in teh direct moisture system component.</summary>
  /// <remarks>Value <c>-1</c> means the moisture override was not setup.</remarks>
  /// <seealso cref="StartMoisturizing"/>
  /// <seealso cref="StopMoisturizing"/>
  int _overrideIndex = -1;

  /// <summary>If set to <c>true</c>, then the irrigated tiles set will be updated on the next tick.</summary>
  /// <remarks>
  /// Set this flag each time when something that may affect irrigate has happen. The update is is not cheap, so don't
  /// trigger it on every tick.
  /// </remarks>
  /// <seealso cref="UpdateState"/>
  bool _tilesLayoutChanged = true;

  /// <summary>The Z level at which this building will irrigate the tiles.</summary>
  /// <remarks>The irrigation won't work above or below.</remarks>
  /// <seealso cref="UpdateBuildingPositioning"/>
  int _baseZ;

  /// <summary>The building approximated center to use when determining the range.</summary>
  /// <remarks>The coordinates may not be integer. It depends on the actual building size.</remarks>
  /// <seealso cref="UpdateBuildingPositioning"/>
  Vector2 _buildingCenter;

  /// <summary>The per hour consumption defined by the consuming building prefab.</summary>
  /// <remarks>It's a base for the <see cref="IrrigationCoverage"/> adjustments.</remarks>
  float _prefabGoodPerHour;

  /// <summary>The last calculated efficiency modifier.</summary>
  float _currentEfficiency = -1f;

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

  void Awake() {
    _blockObject = GetComponentFast<BlockObject>();
    _prefab = GetComponentFast<Prefab>();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _prefabGoodPerHour = GoodPerHourField.Get(_goodConsumingBuilding);
    GetComponentsFast(_efficiencyProviders);
  }

  /// <summary>Updates the eligible tiles and moisture system.</summary>
  /// <remarks>If case of there are changes in the irrigating tiles, the moisturing will be stopped.</remarks>
  /// <seealso cref="_tilesLayoutChanged"/>
  void UpdateState() {
    var needMoistuireSystemUpdate = false;

    // Efficiency affects the irrigated radius.
    var newEfficiency = GetEfficiency();
    if (Mathf.Abs(_currentEfficiency - newEfficiency) > float.Epsilon) {
      HostedDebugLog.Fine(this, "Efficiency changed: {0} => {1}", _currentEfficiency, newEfficiency);
      _currentEfficiency = newEfficiency;
      needMoistuireSystemUpdate = true;
    }

    // Changing in terrine or new/destroyed buildings.
    if (_tilesLayoutChanged) {
      _tilesLayoutChanged = false;
      needMoistuireSystemUpdate = true;
      var oldTilesCount = EligibleTiles?.Count;
      EligibleTiles = GetTiles(useEffectiveRange: false, skipChecks: false);
      HostedDebugLog.Fine(this, "Tiles updated: {0} => {1}", oldTilesCount, EligibleTiles.Count);
      IrrigationCoverage = (float)EligibleTiles.Count / IrrigationMaxCoverage;
      if (IrrigationCoverage > 0) {
        GoodPerHourField.Set(_goodConsumingBuilding, _prefabGoodPerHour * IrrigationCoverage);
      } else {
        // Zero consumption rate causes troubles to the consuming building component.
        GoodPerHourField.Set(_goodConsumingBuilding, _prefabGoodPerHour);
      }
      GetComponentFast<BuildingWithRangeUpdater>().OnPostTransformChanged();
    }

    // Sync the state.
    if (needMoistuireSystemUpdate) {
      StopMoisturizing();
    }
    if (_goodConsumingBuilding.IsConsuming) {
      StartMoisturizing();
    } else {
      StopMoisturizing();
    }
  }

  /// <summary>Starts logic on the irrigated tiles.</summary>
  void StartMoisturizing() {
    if (_overrideIndex != -1) {
      return;
    }
    var irrigatedTiles = GetTiles(useEffectiveRange: true, skipChecks: false);
    _overrideIndex = _directSoilMoistureSystemAccessor.AddMoistureOverride(irrigatedTiles, _moistureLevel);
  }

  /// <summary>Stops any logic on the irrigated tiles.</summary>
  void StopMoisturizing() {
    if (_overrideIndex == -1) {
      return;
    }
    _directSoilMoistureSystemAccessor.RemoveMoistureOverride(_overrideIndex);
    _overrideIndex = -1;
  }

  /// <summary>Returns the current building efficiency modifier.</summary>
  /// <seealso cref="IBuildingEfficiencyProvider"/>
  float GetEfficiency() {
    if (!_blockObject.Finished) {
      return 1f;
    }
    var efficiency = 1f;
    for (var i = 0; i < _efficiencyProviders.Count; i++) {
      efficiency *= _efficiencyProviders[i].Efficiency;
    }
    return efficiency;
  }

  /// <summary>Updates the building size and position to its current state.</summary>
  void UpdateBuildingPositioning() {
    var foundation = _blockObject.PositionedBlocks.GetFoundationCoordinates();
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

    _buildingCenter = new Vector2(minX + dx / 2.0f, minY + dy / 2.0f);
    _radiusAdjuster = Math.Max(dx + 1, dy + 1) / 2.0f;
    _adjustedMaxSqrRadius = (_irrigationRange + _radiusAdjuster) * (_irrigationRange + _radiusAdjuster);
    _baseZ = _blockObject.Placement.Coordinates.z;
  }

  /// <summary>Tells if the provided tile is within the tower's max range.</summary>
  /// <seealso cref="_irrigationRange"/>
  bool IsTileInRange(Vector2Int tile) {
    var sqrDist = (tile.x - _buildingCenter.x) * (tile.x - _buildingCenter.x)
        + (tile.y - _buildingCenter.y) * (tile.y - _buildingCenter.y);
    return sqrDist <= _adjustedMaxSqrRadius;
  }

  /// <summary>Returns all the tiles in the irrigated range.</summary>
  HashSet<Vector2Int> GetTiles(bool useEffectiveRange, bool skipChecks) {
    var tilesToVisit = new Queue<Vector2Int>();
    var visitedTiles = new HashSet<int>();
    var result = new List<Vector2Int>();
    var sqrRadius = useEffectiveRange
        ? (EffectiveRange + _radiusAdjuster) * (EffectiveRange + _radiusAdjuster)
        : _adjustedMaxSqrRadius;

    tilesToVisit.Enqueue(_blockObject.Placement.Coordinates.XY());
    while (!tilesToVisit.IsEmpty()) {
      var tile = tilesToVisit.Dequeue();
      if (!skipChecks) {
        if (tile.x < 0 || tile.x >= _mapIndexService.MapSize.x || tile.y < 0 || tile.y >= _mapIndexService.MapSize.y) {
          continue; // Reject the out of map tiles.
        }
      }
      var index = _mapIndexService.CoordinatesToIndex(tile);
      if (visitedTiles.Contains(index)) {
        continue; // Already checked, skip it.
      }
      visitedTiles.Add(index);
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
    var foundation = _blockObject.PositionedBlocks.GetFoundationCoordinates().Select(c => c.XY()).ToHashSet();
    final.RemoveWhere(x => foundation.Contains(x));
    return final;
  }

  #endregion

  #region Terrain and buildings change callbacks

  /// <summary>Triggers the tiles update if something is built within the range.</summary>
  /// <seealso cref="EligibleTiles"/>
  [OnEvent]
  public void OnConstructibleEnteredFinishedStateEvent(ConstructibleEnteredFinishedStateEvent e) {
    if (IsTileInRange(e.Constructible.GetComponentFast<BlockObject>().Coordinates.XY())) {
      _tilesLayoutChanged = true;
    }
  }

  /// <summary>Triggers the tiles update if something has been destroyed within the range.</summary>
  /// <seealso cref="EligibleTiles"/>
  [OnEvent]
  public void OnEntityDeletedEvent(EntityDeletedEvent e) {
    var constructable = e.Entity.GetComponentFast<Constructible>();
    if (constructable == null || !constructable.IsFinished) {
      return; // The preview objects cannot affect irrigation.
    }
    if (IsTileInRange(e.Entity.GetComponentFast<BlockObject>().Coordinates.XY())) {
      _tilesLayoutChanged = true;
    }
  }

  /// <summary>The terrain changes must trigger the irrigation coverage re-calculation.</summary>
  void OnTerrainHeightChanged(object sender, TerrainHeightChangedEventArgs e) {
    if (IsTileInRange(e.Coordinates)) {
      _tilesLayoutChanged = true;
    }
  }

  #endregion
}
}

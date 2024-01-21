// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
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
using Timberborn.Localization;
using Timberborn.MapIndexSystem;
using Timberborn.PrefabSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilBarrierSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityDev.Utils.Reflections;
using UnityEngine;

//IPostInitializableLoadedEntity
namespace IgorZ.TimberCommons.IrrigationTowerComponent {
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
class IrrigationTower : TickableComponent, IBuildingWithRange, IFinishedStateListener, IPostTransformChangeListener,
                        IConsumptionRateFormatter {
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
  public bool IsIrrigating => _overrideIndex != -1;

  /// <summary>The current irrigation range.</summary>
  /// <remarks>It can change based on the building efficiency providers reports.</remarks>
  /// <seealso cref="IrrigatedTilesCount"/>
  public int EffectiveRange => Mathf.RoundToInt(_irrigationRange * _currentEfficiency);

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

  #region IConsumptionRateFormatter implementation
  const string DaysShortLocKey = "Time.DaysShort";

  public string GetRate() {
    var goodPerHour = GoodPerHourField.Get(_goodConsumingBuilding) * 24;
    return goodPerHour.ToString("0.#");
  }

  public string GetTime() {
    return _loc.T(DaysShortLocKey, "1");
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
    if (!MaxCoverageByRadius.TryGetValue(_irrigationRange, out var maxCoverage)) {
      maxCoverage = GetTiles(range: _irrigationRange, skipChecks: true).Count;
      MaxCoverageByRadius.Add(_irrigationRange, maxCoverage);
      DebugEx.Fine("Calculated max coverage: radius={0}, coverage={1}", _irrigationRange, maxCoverage);
    }
    MaxCoveredTilesCount = maxCoverage;
    _needMoistureSystemUpdate = true;
    UpdateState();
  }
  static readonly Dictionary<int, int> MaxCoverageByRadius = new();

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
  ILoc _loc;
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
  bool _needMoistureSystemUpdate = true;

  /// <summary>The Z level at which this building will irrigate the tiles.</summary>
  /// <remarks>The irrigation won't work above or below.</remarks>
  /// <seealso cref="UpdateBuildingPositioning"/>
  int _baseZ;

  /// <summary>The building approximated center to use when determining the range.</summary>
  /// <remarks>The coordinates may not be integer. It depends on the actual building size.</remarks>
  /// <seealso cref="UpdateBuildingPositioning"/>
  Vector2 _buildingCenter;

  /// <summary>The per hour consumption defined by the consuming building prefab.</summary>
  float _prefabGoodPerHour;

  /// <summary>The last calculated efficiency modifier.</summary>
  float _currentEfficiency = 1.0f;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ITerrainService terrainService, SoilBarrierMap soilBarrierMap,
                                 MapIndexService mapIndexService, EventBus eventBus, ILoc loc,
                                 DirectSoilMoistureSystemAccessor directSoilMoistureSystemAccessor) {
    _terrainService = terrainService;
    _soilBarrierMap = soilBarrierMap;
    _mapIndexService = mapIndexService;
    _eventBus = eventBus;
    _loc = loc;
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
      HostedDebugLog.Fine(this, "Tiles updated: eligible={0}, irrigated={1}", EligibleTilesCount, IrrigatedTilesCount);
      var irrigationCoverage = (float)IrrigatedTilesCount / MaxCoveredTilesCount;
      if (irrigationCoverage > 0) {
        GoodPerHourField.Set(_goodConsumingBuilding, _prefabGoodPerHour * irrigationCoverage);
      } else {
        // Zero consumption rate causes troubles to the consuming building component.
        GoodPerHourField.Set(_goodConsumingBuilding, _prefabGoodPerHour);
      }
      GetComponentFast<BuildingWithRangeUpdater>().OnPostTransformChanged();
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
    var irrigatedTiles = GetTiles(range: EffectiveRange, skipChecks: false);
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
  HashSet<Vector2Int> GetTiles(float range, bool skipChecks) {
    var tilesToVisit = new Queue<Vector2Int>();
    var visitedTiles = new HashSet<Vector2Int>();
    var result = new List<Vector2Int>();
    var sqrRadius = (range + _radiusAdjuster) * (range + _radiusAdjuster);

    tilesToVisit.Enqueue(_blockObject.Placement.Coordinates.XY());
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
    var foundation = _blockObject.PositionedBlocks.GetFoundationCoordinates().Select(c => c.XY()).ToHashSet();
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

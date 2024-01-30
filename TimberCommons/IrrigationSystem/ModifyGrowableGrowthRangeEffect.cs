// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.ConstructibleSystem;
using Timberborn.EntitySystem;
using Timberborn.PrefabSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

/// <summary>A range effect that changes the growables growth rate.</summary>
/// <remarks>
/// <p>
/// The modifier can increase or decrease the growth rate: if <i>less</i> than <c>0</c>, then it's "a moderator";
/// if <i>greater</i> than <c>0</c>, then it's a booster.
/// </p>
/// <p>
/// The ranges can intersect. If tile is in range of multiple growth affects, then the effective rate is found between
/// the minimum growth moderator and the maximum growth booster. The modifiers of the same type do not add up.  
/// </p>
/// </remarks>
/// <seealso cref="GoodConsumingIrrigationTower"/>
/// <seealso cref="ManufactoryIrrigationTower"/>
public sealed class ModifyGrowableGrowthRangeEffect : BaseComponent, IRangeEffect, IFinishedStateListener {

  #region Unity managed fields
  // ReSharper disable InconsistentNaming
  // ReSharper disable RedundantDefaultMemberInitializer

  /// <inheritdoc cref="EffectGroup"/>
  [SerializeField]
  [Tooltip(
      "The name by which this effect can be found by the other components. Multiple effects can have the same name.")]
  internal string _effectGroupName = "ModifyGrowthRate";

  /// <summary>
  /// The modifier percentile to the original tree growth rate. It can increase or decrease the growth rate.
  /// </summary>
  /// <remarks>
  /// Values below <c>0</c> are "moderators", they decrease the growth rate. Values above <c>0</c> are "boosters", they
  /// increase the growth rate.
  /// </remarks>
  [SerializeField]
  [Tooltip("Positive or negative percent value. E.g. '15.5' or '-8.5'.")]
  internal float _growthRateModifier = 0;

  /// <summary>The components that must exist on the growable in order to be a target of this effect.</summary>
  /// <remarks>
  /// <p>If the list is empty, then no restriction by the components is applied.</p>
  /// <p>
  /// The names must be in a full notion, e.g. "<c>Timberborn.Forestry.TreeComponent</c>". The growable will be selected
  /// if <i>any</i> of the components are present on the block object.
  /// </p>
  /// </remarks>
  [SerializeField]
  [Tooltip(
      "Full names of components that must be present on the growable. Leave empty to not check for the components.")]
  internal string[] _componentsFilter = {};

  /// <summary>The exact names of the prefabs to be selected to this effect.</summary>
  /// <remarks>If the list is empty, then no restriction by the prefab name is applied.</remarks>
  [SerializeField]
  [Tooltip("The exact prefab names to apply the effect to.")]
  internal string[] _prefabNamesFilter = {};

  // ReSharper restore InconsistentNaming
  // ReSharper restore RedundantDefaultMemberInitializer
  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public void OnEnterFinishedState() {
    _eventBus.Register(this);
  }

  /// <inheritdoc/>
  public void OnExitFinishedState() {
    _eventBus.Unregister(this);
  }

  #endregion

  #region IRangeEffect implementation

  /// <inheritdoc/>
  public string EffectGroup => _effectGroupName;

  /// <inheritdoc/>
  public void ApplyEffect(ImmutableHashSet<Vector2Int> tiles) {
    ResetEffect();
    _allTiles = tiles;
    _coordsWithBoost = new List<Vector3Int>();
    // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
    foreach (var tile in _allTiles) {
      var coords = AddModifierToTile(tile);
      if (coords.HasValue) {
        _coordsWithBoost.Add(coords.Value);
      }
    }
  }

  /// <inheritdoc/>
  public void ResetEffect() {
    if (_allTiles == null) {
      return;
    }
    foreach (var coords in _coordsWithBoost) {
      RemoveModifierAtCoords(coords);
    }
    _allTiles = null;
    _coordsWithBoost = null;
  }

  #endregion

  #region Implementation

  ITerrainService _terrainService;
  BlockService _blockService;
  EventBus _eventBus;
  string _modifierOwnerId;
  HashSet<string> _requiredComponents;
  HashSet<string> _requiredPrefabNames;

  ImmutableHashSet<Vector2Int> _allTiles;
  List<Vector3Int> _coordsWithBoost;

  /// <inheritdoc/>
  public override string ToString() {
    return $"[{GetType().Name}#{EffectGroup}]";
  }

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ITerrainService terrainService, BlockService blockService, EventBus eventBus) {
    _terrainService = terrainService;
    _blockService = blockService;
    _eventBus = eventBus;
  }

  void Awake() {
    _modifierOwnerId = Guid.NewGuid().ToString();
    _requiredComponents = _componentsFilter.ToHashSet();
    _requiredPrefabNames = _prefabNamesFilter.ToHashSet();
  }

  /// <summary>Adds the rate modifier at the tile, given there is an eligible growable.</summary>
  /// <returns>The exact coordinates of the growable or <c>null</c> if there is none.</returns>
  Vector3Int? AddModifierToTile(Vector2Int tile) {
    var height = _terrainService.CellHeight(tile);
    var coords = new Vector3Int(tile.x, tile.y, height);
    var modifier = _blockService.GetBottomObjectComponentAt<GrowthRateModifier>(coords);
    if (modifier == null || !modifier.IsLiveAndGrowing) {
      return null;
    }
    if (_requiredPrefabNames.Count > 0 && !_requiredPrefabNames.Contains(modifier.GetComponentFast<Prefab>().Name)) {
      return null;
    }
    var hasComponents = _requiredComponents.IsEmpty()
        || modifier.AllComponents.Any(component => _requiredComponents.Contains(component.GetType().FullName));
    if (!hasComponents) {
      return null;
    }
    modifier.RegisterModifier(_modifierOwnerId, _growthRateModifier);

    return coords;
  }

  /// <summary>Removes the modifier a the coordinates or NOOP if no <see cref="GrowthRateModifier"/> is there.</summary>
  void RemoveModifierAtCoords(Vector3Int coords) {
    var modifier = _blockService.GetBottomObjectComponentAt<GrowthRateModifier>(coords);
    if (modifier == null) {
      return;
    }
    modifier.UnregisterModifier(_modifierOwnerId);
  }

  #endregion

  #region Events and callabcks

  /// <summary>Checks if a newly created growable needs to get teh effect.</summary>
  [OnEvent]
  public void OnEntityInitializedEvent(EntityInitializedEvent e) {
    if (_allTiles == null) {
      return;
    }
    var modifier = e.Entity.GetComponentFast<GrowthRateModifier>();
    if (modifier == null) {
      return;
    }
    var affectedTile = modifier.GetComponentFast<BlockObject>().Coordinates.XY();
    if (!_allTiles.Contains(affectedTile)) {
      return;
    }
    var coords = AddModifierToTile(affectedTile);
    if (coords.HasValue) {
      _coordsWithBoost.Add(coords.Value);
    }
  }

  #endregion
}

}

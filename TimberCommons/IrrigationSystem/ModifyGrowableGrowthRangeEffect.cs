// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>A range effect that changes the growables growth rate.</summary>
/// <remarks>
/// <p>
/// The modifier can increase or decrease the growth rate: if <i>less</i> than <c>0</c>, then it is "a moderator";
/// if <i>greater</i> than <c>0</c>, then it is a booster.
/// </p>
/// <p>
/// The ranges can intersect. If tile is in range of multiple growth affects, then the effective rate is found between
/// the minimum growth moderator and the maximum growth booster. The modifiers of the same type don't add up.  
/// </p>
/// </remarks>
/// <seealso cref="GoodConsumingIrrigationTower"/>
/// <seealso cref="ManufactoryIrrigationTower"/>
public sealed class ModifyGrowableGrowthRangeEffect
    : BaseComponent, IAwakableComponent, IRangeEffect, IFinishedStateListener {

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
  public string EffectGroup { get; private set; }

  /// <inheritdoc/>
  public void ApplyEffect(HashSet<Vector3Int> tiles) {
    ResetEffect();
    _allTiles = tiles;
    _coordsWithBoost = [];
    // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
    foreach (var tile in _allTiles) {
      if (MaybeAddModifierToTile(tile)) {
        _coordsWithBoost.Add(tile);
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

  BlockService _blockService;
  EventBus _eventBus;
  string _modifierOwnerId;
  HashSet<string> _requiredComponents;
  HashSet<string> _requiredPrefabNames;
  float _growthRateModifier;

  HashSet<Vector3Int> _allTiles;
  List<Vector3Int> _coordsWithBoost;

  /// <inheritdoc/>
  public override string ToString() {
    return $"[{GetType().Name}#{EffectGroup}]";
  }

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(BlockService blockService, EventBus eventBus) {
    _blockService = blockService;
    _eventBus = eventBus;
  }

  /// <inheritdoc/>
  public void Awake() {
    _modifierOwnerId = Guid.NewGuid().ToString();
    var effectSpec = GetComponent<ModifyGrowableGrowthRangeEffectSpec>();
    _requiredComponents = effectSpec.ComponentsFilter.ToHashSet();
    _requiredPrefabNames = effectSpec.PrefabNamesFilter.ToHashSet();
    _growthRateModifier = effectSpec.GrowthRateModifier;
    EffectGroup = effectSpec.EffectGroup;
  }

  /// <summary>Adds the rate modifier at the tile, given there is eligible growable.</summary>
  bool MaybeAddModifierToTile(Vector3Int coords) {
    var modifier = _blockService.GetBottomObjectComponentAt<GrowthRateModifier>(coords);
    if (!modifier || !modifier.IsLiveAndGrowing) {
      return false;
    }
    if (_requiredPrefabNames.Count > 0 && !_requiredPrefabNames.Contains(modifier.Name)) {
      return false;
    }
    var hasComponents = _requiredComponents.IsEmpty()
        || modifier.AllComponents.Any(component => _requiredComponents.Contains(component.GetType().FullName));
    if (!hasComponents) {
      return false;
    }
    modifier.RegisterModifier(_modifierOwnerId, _growthRateModifier);

    return true;
  }

  /// <summary>Removes the modifier at coordinates or NOOP if no <see cref="GrowthRateModifier"/> is there.</summary>
  void RemoveModifierAtCoords(Vector3Int coords) {
    var modifier = _blockService.GetBottomObjectComponentAt<GrowthRateModifier>(coords);
    if (modifier == null) {
      return;
    }
    modifier.UnregisterModifier(_modifierOwnerId);
  }

  #endregion

  #region Events and callabcks

  /// <summary>Checks if a newly created growable needs to get the effect.</summary>
  [OnEvent]
  public void OnEntityInitializedEvent(EntityInitializedEvent e) {
    if (_allTiles == null) {
      return;
    }
    var modifier = e.Entity.GetComponent<GrowthRateModifier>();
    if (!modifier) {
      return;
    }
    var affectedTile = modifier.GetComponent<BlockObject>().Coordinates;
    if (!_allTiles.Contains(affectedTile)) {
      return;
    }
    if (MaybeAddModifierToTile(affectedTile)) {
      _coordsWithBoost.Add(affectedTile);
    }
  }

  #endregion
}
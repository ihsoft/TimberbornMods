// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BaseComponentSystem;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>A range effect that blocks the contamination on the tiles.</summary>
/// <remarks>
/// The ranges can intersect. Tiles in the intersections will be properly handled when one of the effects is removed.
/// </remarks>
/// <seealso cref="GoodConsumingIrrigationTower"/>
/// <seealso cref="ManufactoryIrrigationTower"/>
/// <seealso cref="SoilOverridesService"/>
public sealed class BlockContaminationRangeEffect : BaseComponent, IRangeEffect, IPersistentEntity {

  #region Unity managed fields
  // ReSharper disable InconsistentNaming

  /// <inheritdoc cref="EffectGroup"/>
  [SerializeField]
  [Tooltip(
    "The name by which this effect can be found by the other components. Multiple effects can have the same name.")]
  string _effectGroupName = "BlockContamination";

  // ReSharper restore InconsistentNaming
  #endregion

  #region IRangeEffect implementation

  /// <inheritdoc/>
  public string EffectGroup => _effectGroupName;

  /// <inheritdoc/>
  public void ApplyEffect(HashSet<Vector3Int> tiles) {
    ResetEffect();
    _contaminationOverrideIndex = _soilOverridesService.AddContaminationOverride(tiles);
  }

  /// <inheritdoc/>
  public void ResetEffect() {
    if (_contaminationOverrideIndex == -1) {
      return;
    }
    _soilOverridesService.RemoveContaminationOverride(_contaminationOverrideIndex);
    _contaminationOverrideIndex = -1;
  }

  #endregion

  #region Implementation

  SoilOverridesService _soilOverridesService;
  int _contaminationOverrideIndex = -1;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(SoilOverridesService soilOverridesService) {
    _soilOverridesService = soilOverridesService;
  }

  /// <inheritdoc/>
  public override string ToString() {
    return $"[{GetType().Name}#{EffectGroup}]";
  }

  #endregion

  #region IPersistentEntity implementation

  static readonly ComponentKey ComponentKey = new(typeof(BlockContaminationRangeEffect).FullName);
  static readonly PropertyKey<int> ContaminationOverrideIndexKey = new("OverrideIndex");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    var component = entitySaver.GetComponent(ComponentKey);
    component.Set(ContaminationOverrideIndexKey, _contaminationOverrideIndex);
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(ComponentKey, out var component)) {
      return;
    }
    _contaminationOverrideIndex = component.Get(ContaminationOverrideIndexKey);
    if (_contaminationOverrideIndex != -1) {
      _soilOverridesService.ClaimContaminationOverrideIndex(_contaminationOverrideIndex);
    }
  }

  #endregion
}
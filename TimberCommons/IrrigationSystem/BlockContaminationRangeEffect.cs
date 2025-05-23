// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BaseComponentSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>A range effect that blocks the contamination on the tiles.</summary>
/// <remarks>
/// The ranges can intersect. Tiles in the intersections will be properly handled when one of the effects is removed.
/// </remarks>
/// <seealso cref="GoodConsumingIrrigationTower"/>
/// <seealso cref="ManufactoryIrrigationTower"/>
/// <seealso cref="SoilOverridesService"/>
public sealed class BlockContaminationRangeEffect : BaseComponent, IRangeEffect {

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

  #endregion

  /// <inheritdoc/>
  public override string ToString() {
    return $"[{GetType().Name}#{EffectGroup}]";
  } 
}
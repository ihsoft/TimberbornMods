// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.WaterService;
using Timberborn.BaseComponentSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

/// <summary>Range effect blocks contamination on the tiles.</summary>
/// <remarks>
/// The ranges can intersect. Tiles in the intersections will be properly handled when one of the effects is removed.
/// </remarks>
/// <seealso cref="DirectSoilMoistureSystemAccessor"/>
public sealed class BlockContaminationRangeEffect : BaseComponent, IRangeEffect {
  #region Unity managed fields

  [SerializeField]
  // ReSharper disable once InconsistentNaming
  string _effectGroupName = "BlockContamination";

  #endregion

  #region IRangeEffect implementation

  /// <inheritdoc/>
  public string EffectGroup => _effectGroupName;

  /// <inheritdoc/>
  public void ApplyEffect(IEnumerable<Vector2Int> tiles) {
    RestEffect();
    _contaminationOverrideIndex = _directSoilMoistureSystemAccessor.AddContaminationOverride(tiles);
  }

  /// <inheritdoc/>
  public void RestEffect() {
    if (_contaminationOverrideIndex == -1) {
      return;
    }
    _directSoilMoistureSystemAccessor.RemoveContaminationOverride(_contaminationOverrideIndex);
    _contaminationOverrideIndex = -1;
  }

  #endregion

  #region Implementation

  DirectSoilMoistureSystemAccessor _directSoilMoistureSystemAccessor;
  int _contaminationOverrideIndex = -1;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(DirectSoilMoistureSystemAccessor directSoilMoistureSystemAccessor) {
    _directSoilMoistureSystemAccessor = directSoilMoistureSystemAccessor;
  }

  #endregion
}
}

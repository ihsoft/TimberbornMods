// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using Timberborn.Buildings;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.Localization;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

/// <summary>Irrigation tower that runs on top of `GoodConsumingBuilding`.</summary>
/// <remarks>
/// <p>
/// The building is responsible for dealing with the goods (e.g. water). The tower will keep tiles in range irrigated as
/// long as the building is consuming fuel (i.e. it's active).
/// </p>
/// <p>
/// If building has components, implementing <see cref="IRangeEffect"/>, then they will be applied one the irrigation
/// started. The <see cref="IRangeEffect.EffectGroup"/> is not considered, all effects will be used. 
/// </p>
/// </remarks>
public class GoodConsumingIrrigationTower : IrrigationTower, IConsumptionRateFormatter {

  #region IConsumptionRateFormatter implementation
  
  const string DaysShortLocKey = "Time.DaysShort";

  /// <inheritdoc/>
  public string GetRate() {
    var goodPerHour = _goodConsumingBuilding._goodPerHour * 24;
    return goodPerHour.ToString("0.#");
  }
  
  /// <inheritdoc/>
  public string GetTime() {
    return _loc.T(DaysShortLocKey, "1");
  }
  
  #endregion

  #region IrrigationTower overrides

  /// <inheritdoc/>
  protected override bool CanMoisturize() {
    return _goodConsumingBuilding.CanUse && !_goodConsumingBuilding.ConsumptionPaused;
  }

  /// <inheritdoc/>
  protected override void IrrigationStarted(IEnumerable<Vector2Int> tiles) {
    _rangeEffects.ForEach(x => x.ApplyEffect(tiles));
  }

  /// <inheritdoc/>
  protected override void IrrigationStopped() {
    _rangeEffects.ForEach(x => x.RestEffect());
  }

  /// <inheritdoc/>
  protected override void UpdateConsumptionRate() {
    if (Coverage > 0) {
      _goodConsumingBuilding._goodPerHour = _prefabGoodPerHour * Coverage;
    } else {
      // Zero consumption rate causes troubles to the consuming building component.
      _goodConsumingBuilding._goodPerHour = _prefabGoodPerHour;
    }
  }

  /// <inheritdoc/>
  protected override float GetEfficiency() {
    if (!BlockObject.Finished) {
      return 1f;
    }
    var efficiency = 1f;
    // ReSharper disable once ForCanBeConvertedToForeach
    // ReSharper disable once LoopCanBeConvertedToQuery
    for (var i = 0; i < _efficiencyProviders.Count; i++) {
      efficiency *= _efficiencyProviders[i].Efficiency;
    }
    return efficiency;
  }

  #endregion

  #region Implementation

  ILoc _loc;
  GoodConsumingBuilding _goodConsumingBuilding;
  readonly List<IBuildingEfficiencyProvider> _efficiencyProviders = new();
  readonly List<IRangeEffect> _rangeEffects = new();
  float _prefabGoodPerHour;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  protected override void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _prefabGoodPerHour = _goodConsumingBuilding._goodPerHour;
    GetComponentsFast(_efficiencyProviders);
    GetComponentsFast(_rangeEffects);
  }

  #endregion
}

}

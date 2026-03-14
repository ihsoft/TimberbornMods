// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using Timberborn.Buildings;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.Localization;
using Timberborn.UIFormatters;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>Irrigation tower that runs on top of <see cref="GoodConsumingBuilding"/>.</summary>
/// <remarks>
/// <p>
/// The building is responsible for dealing with the goods (for example, water). The tower will keep tiles in range
/// irrigated as long as the building is consuming fuel (in other words, it is active).
/// </p>
/// <p>
/// If building has components, implementing <see cref="IRangeEffect"/>, then they will be applied once the irrigation
/// started. The <see cref="IRangeEffect.EffectGroup"/> is not considered, all effects will be used. 
/// </p>
/// </remarks>
public class GoodConsumingIrrigationTower : IrrigationTower, IConsumptionRateFormatter {

  #region IConsumptionRateFormatter implementation

  /// <inheritdoc/>
  public string GetRate() {
    var consumedGood = _goodConsumingBuilding._goodConsumingBuildingSpec.ConsumedGoods[0];
    var goodPerHour = consumedGood.GoodPerHour * 24;
    return goodPerHour.ToString("0.#");
  }
  
  /// <inheritdoc/>
  public string GetTime() {
    return UnitFormatter.FormatDays("1", _loc);
  }
  
  #endregion

  #region IrrigationTower overrides

  /// <inheritdoc/>
  protected override int IrrigationRange => _irrigationRange;
  int _irrigationRange;

  /// <inheritdoc/>
  protected override bool IrrigateFromGroundTilesOnly => _irrigateFromGroundTilesOnly;
  bool _irrigateFromGroundTilesOnly;

  /// <inheritdoc/>
  protected override bool CanMoisturize() {
    return _goodConsumingBuilding.CanUse && !_goodConsumingBuilding.ConsumptionPaused;
  }

  /// <inheritdoc/>
  protected override void IrrigationStarted() {
    _rangeEffects.ForEach(x => x.ApplyEffect(ReachableTiles));
  }

  /// <inheritdoc/>
  protected override void IrrigationStopped() {
    _rangeEffects.ForEach(x => x.ResetEffect());
  }

  /// <inheritdoc/>
  protected override void UpdateConsumptionRate() {
    var newConsumptionRate = _prefabConsumedGoodSpec.GoodPerHour * Coverage;
    if (Math.Abs(_prefabConsumedGoodSpec.GoodPerHour - newConsumptionRate) > float.Epsilon) {
      var newRateSpec = _prefabGoodConsumingBuildingSpec with {
          ConsumedGoods = [_prefabConsumedGoodSpec with { GoodPerHour = newConsumptionRate }],
      };
      HostedDebugLog.Fine(this, "Updating consumption rate spec: from={0}, to {1}",
                          _goodConsumingBuilding._goodConsumingBuildingSpec, newRateSpec);
      _goodConsumingBuilding._goodConsumingBuildingSpec = newRateSpec;
    }

    // Lazy init to not depend on the initialization order.
    _goodConsumingToggle ??= _goodConsumingBuilding.GetGoodConsumingToggle();
    if (Coverage > 0) {
      _goodConsumingToggle.ResumeConsumption();
    } else {
      _goodConsumingToggle.PauseConsumption();
    }
  }

  /// <inheritdoc/>
  protected override float GetEfficiency() {
    if (!BlockObject.IsFinished) {
      return 1f;
    }
    var efficiency = 1f;
    // ReSharper disable once ForCanBeConvertedToForeach
    // ReSharper disable once LoopCanBeConvertedToQuery
    for (var i = _efficiencyProviders.Count - 1; i >= 0; i--) {
      efficiency *= _efficiencyProviders[i].Efficiency;
    }
    return efficiency;
  }

  #endregion

  #region Implementation

  ILoc _loc;
  GoodConsumingBuildingSpec _prefabGoodConsumingBuildingSpec;
  ConsumedGoodSpec _prefabConsumedGoodSpec;
  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;
  readonly List<IBuildingEfficiencyProvider> _efficiencyProviders = [];
  readonly List<IRangeEffect> _rangeEffects = [];

  /// <summary>It must be public for the injection logic to work.</summary>
  /// FIXME: to constructor?
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  /// <inheritdoc/>
  public override void Awake() {
    base.Awake();
    _goodConsumingBuilding = GetComponent<GoodConsumingBuilding>();
    var goodConsumingIrrigationTowerSpec = GetComponent<GoodConsumingIrrigationTowerSpec>();
    _irrigationRange = goodConsumingIrrigationTowerSpec.IrrigationRange;
    _irrigateFromGroundTilesOnly = goodConsumingIrrigationTowerSpec.IrrigateFromGroundTilesOnly;
    _prefabGoodConsumingBuildingSpec = GetComponent<GoodConsumingBuildingSpec>();
    if (_prefabGoodConsumingBuildingSpec.ConsumedGoods.Length != 1) {
      throw new InvalidOperationException(
          $"Towers can work with one consumed good only. Spec: {_prefabGoodConsumingBuildingSpec}");
    }
    _prefabConsumedGoodSpec = _prefabGoodConsumingBuildingSpec.ConsumedGoods[0];
    GetComponents(_efficiencyProviders);
    GetComponents(_rangeEffects);
  }

  #endregion
}
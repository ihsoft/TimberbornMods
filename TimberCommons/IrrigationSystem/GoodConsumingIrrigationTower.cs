﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using Timberborn.Buildings;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.Localization;

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
  
  const string DaysShortLocKey = "Time.DaysShort";

  /// <inheritdoc/>
  public string GetRate() {
    var goodPerHour = _goodConsumingBuildingSpec.GoodPerHour * 24;
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
  protected override void IrrigationStarted() {
    _rangeEffects.ForEach(x => x.ApplyEffect(ReachableTiles));
  }

  /// <inheritdoc/>
  protected override void IrrigationStopped() {
    _rangeEffects.ForEach(x => x.ResetEffect());
  }

  /// <inheritdoc/>
  protected override void UpdateConsumptionRate() {
    _goodConsumingBuildingSpec._goodPerHour = _prefabGoodPerHour * Coverage;
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
  GoodConsumingBuildingSpec _goodConsumingBuildingSpec;
  GoodConsumingBuilding _goodConsumingBuilding;
  GoodConsumingToggle _goodConsumingToggle;
  readonly List<IBuildingEfficiencyProvider> _efficiencyProviders = new();
  readonly List<IRangeEffect> _rangeEffects = new();
  float _prefabGoodPerHour;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ILoc loc) {
    _loc = loc;
  }

  /// <inheritdoc/>
  protected override void Awake() {
    base.Awake();
    _goodConsumingBuildingSpec = GetComponentFast<GoodConsumingBuildingSpec>();
    _prefabGoodPerHour = _goodConsumingBuildingSpec.GoodPerHour;
    GetComponentsFast(_efficiencyProviders);
    GetComponentsFast(_rangeEffects);
  }

  /// <inheritdoc/>
  public override void StartTickable() {
    _goodConsumingBuilding = GetComponentFast<GoodConsumingBuilding>();
    _goodConsumingToggle = _goodConsumingBuilding.GetGoodConsumingToggle();
    base.StartTickable();
  }

  #endregion
}
// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.TimberCommons.Common;
using IgorZ.TimberDev.UI;
using Timberborn.Common;
using Timberborn.Localization;
using Timberborn.TimeSystem;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>Irrigation tower that runs on top of <see cref="Manufactory"/>.</summary>
/// <remarks>
/// <p>
/// The manufactory is responsible for dealing with the goods (for example, water). The tower will be the production
/// executor for the manufactory, so no other executors must be setup (for example, workplace or
/// <see cref="ProductionIncreaser"/>). The tower will keep the tiles in range irrigated as long as the manufactory can
/// produce (<see cref="Manufactory.IsReadyToProduce"/>).
/// </p>
/// <p>
/// If building has components, implementing <see cref="IRangeEffect"/>, then they will be applied based on the
/// currently selected recipe (<see cref="Manufactory.CurrentRecipe"/>). For this, define the effect field.
/// </p>
/// </remarks>
public class ManufactoryIrrigationTower : IrrigationTower, ISupplyLeftProvider {

  const string NoTilesToIrrigateLocKey = "IgorZ.TimberCommons.WaterTower.NoTilesToIrrigate";

  #region TickableComponent overrides

  /// <inheritdoc/>
  public override void Tick() {
    if (CanMoisturize()) {
      _manufactory.IncreaseProductionProgress(_dayNightCycle.FixedDeltaTimeInHours);
      UpdateConsumptionStats();
    }
    base.Tick();
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
    return BlockableObject.IsUnblocked && _manufactory.IsReadyToProduce;
  }

  /// <inheritdoc/>
  protected override void IrrigationStarted() {
    StartEffectsForCurrentRecipe();
  }

  /// <inheritdoc/>
  protected override void IrrigationStopped() {
    StopEffectsForRecipe(_manufactory.CurrentRecipe?.Id);
  }

  /// <inheritdoc/>
  protected override void UpdateConsumptionRate() {
    UpdateCurrentRecipeDuration();
  }

  /// <inheritdoc/>
  protected override float GetEfficiency() {
    return _manufactory.IsReadyToProduce ? _manufactory.ProductionEfficiency() : 0;
  }

  /// <inheritdoc/>
  protected override void Initialize() {
    base.Initialize();
    _originalRecipeId = _manufactory.CurrentRecipe?.Id;

    // Make effect cache.
    var rangeEffects = new List<IRangeEffect>();
    GetComponents(rangeEffects);
    _availableEffectsDict = rangeEffects
        .Where(x => _effectsRulesDict.ContainsValue(x.EffectGroup))
        .GroupBy(x => x.EffectGroup)
        .ToDictionary(x => x.Key, x => x.ToList());
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public override void OnEnterFinishedState() {
    base.OnEnterFinishedState();
    _manufactory.RecipeChanged += OnProductionRecipeChanged;
  }

  /// <inheritdoc/>
  public override void OnExitFinishedState() {
    base.OnExitFinishedState();
    _manufactory.RecipeChanged -= OnProductionRecipeChanged;
  }

  #endregion

  #region ISupplyLeftProvider implementation

  /// <inheritdoc/>
  public (float progress, string progressBarMsg) GetStats() {
    return (_ingredientsConsumptionProgress, _progressUpdateMsg);
  }

  #endregion

  #region Implemenation

  Manufactory _manufactory;
  IDayNightCycle _dayNightCycle;
  ILoc _loc;

  Dictionary<string, string> _effectsRulesDict;
  Dictionary<string, List<IRangeEffect>> _availableEffectsDict;
  static readonly List<IRangeEffect> NoEffects = [];
  float _ingredientsConsumptionProgress;
  string _progressUpdateMsg;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(IDayNightCycle dayNightCycle, ILoc loc) {
    _dayNightCycle = dayNightCycle;
    _loc = loc;
  }

  /// <inheritdoc/>
  public override void Awake() {
    base.Awake();
    _manufactory = GetComponent<Manufactory>();
    var spec = GetComponent<ManufactoryIrrigationTowerSpec>();
    _irrigationRange = spec.IrrigationRange;
    _irrigateFromGroundTilesOnly = spec.IrrigateFromGroundTilesOnly;
    try {
      _effectsRulesDict = spec.Effects.Select(pair => pair.Split(['='], 2)).ToDictionary(k => k[0], v => v[1]);
    } catch (Exception ex) {
      HostedDebugLog.Error(this, "Cannot parse effects definition: {0}. {1}", spec.Effects, ex);
      throw;
    }
  }

  /// <summary>Returns all effects that should be active for the recipe.</summary>
  /// <seealso cref="ManufactoryIrrigationTowerSpec.Effects"/>
  List<IRangeEffect> GetEffectsForRecipe(string recipeId) {
    if (_effectsRulesDict.TryGetValue(recipeId, out var effectGroup)
        && _availableEffectsDict.TryGetValue(effectGroup, out var effects)) {
      return effects;
    }
    return NoEffects;
  }

  /// <summary>Resets all effects that have started for the recipe.</summary>
  void StopEffectsForRecipe(string recipeId) {
    if (recipeId != null) {
      var effects = GetEffectsForRecipe(recipeId);
      if (!effects.IsEmpty()) {
        HostedDebugLog.Fine(this, "Reset effects: recipeId={0}, effectsNum={1}", recipeId, effects.Count);
        GetEffectsForRecipe(recipeId).ForEach(x => x.ResetEffect());
      }
    }
  }

  /// <summary>Applies effects that are defined for the currently selected recipe.</summary>
  void StartEffectsForCurrentRecipe() {
    if (_manufactory.HasCurrentRecipe && IsIrrigating) {
      var recipeId = _manufactory.CurrentRecipe.Id;
      var effects = GetEffectsForRecipe(recipeId);
      if (!effects.IsEmpty()) {
        HostedDebugLog.Fine(this, "Apply effects: recipeId={0}, effectsNum={1}", recipeId, effects.Count);
        effects.ForEach(x => x.ApplyEffect(ReachableTiles));
      }
    }
  }

  /// <summary>Updates to the new manufactory recipe.</summary>
  /// <param name="oldRecipeId">The recipe that was set before the change.</param>
  void UpdateRecipe(string oldRecipeId) {
    StopEffectsForRecipe(oldRecipeId);
    StartEffectsForCurrentRecipe();
    UpdateCurrentRecipeDuration();
  }

  /// <summary>Adjusts the current recipe duration to match the tower utilization.</summary>
  /// <seealso cref="IrrigationTower.Coverage"/>
  void UpdateCurrentRecipeDuration() {
    var currentRecipeId = _manufactory.CurrentRecipe?.Id;
    if (currentRecipeId == null) {
      UpdateConsumptionStats();
      return;
    }
    var originalRecipe = _manufactory.ProductionRecipes.First(x => x.Id == currentRecipeId);
    var adjustingRatio = Coverage > 0 ? Coverage : 1f;
    var adjustedCycleDurationInHours = originalRecipe.CycleDurationInHours / adjustingRatio;
    var modifiedRecipe = originalRecipe with { CycleDurationInHours = adjustedCycleDurationInHours };
    _manufactory.CurrentRecipe = modifiedRecipe;
    HostedDebugLog.Fine(
      this, "Adjusted recipe duration: id={0}, original={1}, new={2}", originalRecipe.Id,
      originalRecipe.CycleDurationInHours, adjustedCycleDurationInHours);
    UpdateConsumptionStats();
  }

  /// <summary>Updates the "lasts for" stats for the UI fragment.</summary>
  /// <remarks>It must be called each time the current recipe or consumption is updated.</remarks>
  void UpdateConsumptionStats() {
    var recipe = _manufactory.CurrentRecipe;
    if (recipe == null) {
      _ingredientsConsumptionProgress = -1;
      _progressUpdateMsg = null;
      return; // Cannot estimate.
    }
    var inventory = _manufactory.Inventory;
    var supply = new List<(float supplyLeft, float maxSupply)>();
    var ingridentLeftRatio = _manufactory.IsReadyToProduce ? 1f - _manufactory.ProductionProgress : 0f;
    foreach (var ingredient in recipe.Ingredients) {
      var goodRemaining = inventory.AmountInStock(ingredient.Id) + ingredient.Amount * ingridentLeftRatio;
      var consumePerHour = ingredient.Amount / recipe.CycleDurationInHours;
      var supplyLeftHours = goodRemaining / consumePerHour;
      var supplyAtMaxCapacity = inventory.LimitedAmount(ingredient.Id) / consumePerHour;
      supply.Add((supplyLeftHours, supplyAtMaxCapacity));
    }
    if (recipe.ConsumesFuel) {
      var fuelRemaining = inventory.AmountInStock(recipe.Fuel) + _manufactory.FuelRemaining;
      var consumePerHour = 1f / (recipe.CyclesFuelLasts * recipe.CycleDurationInHours);
      var supplyLeftHours = fuelRemaining / consumePerHour;
      var supplyAtMaxCapacity = inventory.LimitedAmount(recipe.Fuel) / consumePerHour;
      supply.Add((supplyLeftHours, supplyAtMaxCapacity));
    }

    var minReserve = supply.OrderBy(x => x.supplyLeft).First();
    _ingredientsConsumptionProgress = minReserve.supplyLeft / minReserve.maxSupply;
    _progressUpdateMsg = Coverage > float.Epsilon
        ? CommonFormats.FormatSupplyLeft(_loc, minReserve.supplyLeft)
        : _loc.T(NoTilesToIrrigateLocKey);
  }

  #endregion

  #region Manufactory production callbacks

  /// <summary>Reacts on the recipe change to update its duration.</summary>
  /// <remarks>It recalls the previous recipe ID.</remarks>
  void OnProductionRecipeChanged(object sender, EventArgs e) {
    var oldRecipeId = _originalRecipeId;
    _originalRecipeId = _manufactory.CurrentRecipe?.Id;
    UpdateRecipe(oldRecipeId);
  }
  string _originalRecipeId;

  #endregion
}
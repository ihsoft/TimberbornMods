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
using Timberborn.Goods;
using Timberborn.Localization;
using Timberborn.TimeSystem;
using Timberborn.Workshops;
using UnityDev.Utils.LogUtilsLite;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem {

/// <summary>Irrigation tower that runs on top of <see cref="Manufactory"/>.</summary>
/// <remarks>
/// <p>
/// The manufactory is responsible for dealing with the goods (e.g. water). The tower will be the production executor
/// for the manufactory, so no other executors must be setup (e.g. workplace or <see cref="ProductionIncreaser"/>). The
/// tower will keep the tiles in range irrigated as long as the manufactory can produce
/// (<see cref="Manufactory.IsReadyToProduce"/>).
/// </p>
/// <p>
/// If building has components, implementing <see cref="IRangeEffect"/>, then they will be applied based on the
/// currently selected recipe (<see cref="Manufactory.CurrentRecipe"/>). For this, define the effects field.
/// </p>
/// </remarks>
public class ManufactoryIrrigationTower : IrrigationTower, ISupplyLeftProvider {

  #region Unity controlled fields
  // ReSharper disable InconsistentNaming
  // ReSharper disable RedundantDefaultMemberInitializer

  /// <summary>Defines rules to apply an effect group per the recipe selected.</summary>
  /// <remarks>Each row is mappings like: <c>&lt;recipe id>=&lt;effect group></c>. The keys must be unique.</remarks>
  /// <see cref="IRangeEffect.EffectGroup"/>
  [SerializeField]
  [Tooltip("Format: RecipeId=EffectName. Recipe IDs in the list must be unique.")]
  internal string[] _effects = {};

  // ReSharper restore InconsistentNaming
  // ReSharper restore RedundantDefaultMemberInitializer
  #endregion

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
  protected override bool CanMoisturize() {
    return BlockableBuilding.IsUnblocked && _manufactory.IsReadyToProduce;
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
    return _manufactory.ProductionEfficiency();
  }

  /// <inheritdoc/>
  protected override void Initialize() {
    base.Initialize();
    _originalRecipeId = _manufactory.CurrentRecipe?.Id;
  }

  #endregion

  #region IFinishedStateListener implementation

  /// <inheritdoc/>
  public override void OnEnterFinishedState() {
    base.OnEnterFinishedState();
    _manufactory.ProductionRecipeChanged += OnProductionRecipeChanged;
  }

  /// <inheritdoc/>
  public override void OnExitFinishedState() {
    base.OnExitFinishedState();
    _manufactory.ProductionRecipeChanged -= OnProductionRecipeChanged;
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
  static readonly List<IRangeEffect> NoEffects = new();
  float _ingredientsConsumptionProgress;
  string _progressUpdateMsg;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(IDayNightCycle dayNightCycle, ILoc loc) {
    _dayNightCycle = dayNightCycle;
    _loc = loc;
  }

  /// <inheritdoc/>
  protected override void Awake() {
    base.Awake();
    _manufactory = GetComponentFast<Manufactory>();
    _effectsRulesDict = _effects
        .Select(pair => pair.Split(new[] {'='}, 2))
        .ToDictionary(k => k[0], v => v[1]);

    // Make effects cache.
    var rangeEffects = new List<IRangeEffect>();
    GetComponentsFast(rangeEffects);
    _availableEffectsDict = rangeEffects
        .Where(x => _effectsRulesDict.ContainsValue(x.EffectGroup))
        .GroupBy(x => x.EffectGroup).ToDictionary(x => x.Key, x => x.ToList());
  }

  /// <summary>Returns all effects that should be active for the recipe.</summary>
  /// <seealso cref="_effects"/>
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
        GetEffectsForRecipe(recipeId).ForEach(x => x.ApplyEffect(ReachableTiles));
      }
    }
  }

  /// <summary>Updates to the new manufactory recipe.</summary>
  /// <param name="oldRecipeId">The recipe that was set before the change.</param>
  void UpdateRecipe(string oldRecipeId) {
    if (_manufactory.CurrentRecipe?.Id == oldRecipeId) {
      return;
    }
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
    var modifiedRecipe = new RecipeSpecification(
        originalRecipe.Id, originalRecipe.BackwardCompatibleIds, originalRecipe.DisplayLocKey,
        adjustedCycleDurationInHours, originalRecipe.CyclesCapacity, originalRecipe.Ingredients,
        originalRecipe.Products, originalRecipe.ProducedSciencePoints, originalRecipe.Fuel,
        originalRecipe.CyclesFuelLasts, originalRecipe.FuelCapacity, originalRecipe.Icon,
        originalRecipe.RequiredFeatureToggle);
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
    var totalGoodsInRecipe = recipe.Ingredients.Count + (recipe.ConsumesFuel ? 1 : 0);
    var supplyLeftHours = new float[totalGoodsInRecipe];
    var supplyAtMaxCapacity = new float[totalGoodsInRecipe];
    for (var index = 0; index < recipe.Ingredients.Count; index++) {
      var ingredient = recipe.Ingredients[index];
      var stock = inventory.AmountInStock(ingredient.GoodId);
      var consumePerHour = ingredient.Amount / recipe.CycleDurationInHours;
      supplyLeftHours[index] = stock / consumePerHour;
      supplyAtMaxCapacity[index] = inventory.LimitedAmount(ingredient.GoodId) / consumePerHour;
    }
    if (recipe.ConsumesFuel) {
      var index = supplyLeftHours.Length - 1;
      var stock = inventory.AmountInStock(recipe.Fuel.Id) + _manufactory.FuelRemaining;
      var consumePerHour = 1f / (recipe.CyclesFuelLasts * recipe.CycleDurationInHours);
      supplyLeftHours[index] = stock / consumePerHour;
      supplyAtMaxCapacity[index] = inventory.LimitedAmount(recipe.Fuel.Id) / consumePerHour;
    }

    var minLastsHours = supplyLeftHours.Min();
    var maxReserveHours = supplyAtMaxCapacity.Min();
    _ingredientsConsumptionProgress = minLastsHours / maxReserveHours;
    _progressUpdateMsg = CommonFormats.FormatSupplyLeft(_loc, minLastsHours);
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

}

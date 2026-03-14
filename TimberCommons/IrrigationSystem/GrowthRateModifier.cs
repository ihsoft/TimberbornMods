// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.Growing;
using Timberborn.TimeSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.IrrigationSystem;

/// <summary>Component that can change the growth rate on the "growable" entities.</summary>
/// <remarks>
/// Don't add to prefab! It's added to every growable entity in the game. Any code can access it and register its own
/// modifications rates. However, only the worst "moderator" and the best "boost" will be considered. The effects are
/// not adding up.
/// </remarks>
/// <seealso cref="ModifyGrowableGrowthRangeEffect"/>
public sealed class GrowthRateModifier : BaseComponent, IAwakableComponent {

  #region API properties
  // ReSharper disable InconsistentNaming
  // ReSharper disable MemberCanBePrivate.Global

  /// <summary>The actual modifier to the growth rate as a positive or negative percent value.</summary>
  /// <seealso cref="WorstModerator"/>
  /// <seealso cref="BestBooster"/>
  public float EffectiveModifier { get; private set; }

  /// <summary>Indicates that growable is actively growing, so the rate modifier makes sense.</summary>
  public bool IsLiveAndGrowing => !_growable.IsGrown && !_growable._livingNaturalResource.IsDead;

  /// <summary>Specifies if the growable is growing or has grown at the non-stock rate.</summary>
  public bool RateIsModified { get; private set; }

  /// <summary>The <i>worst</i> moderator rate modifier.</summary>
  public float WorstModerator { get; private set; }

  /// <summary>The <i>best</i> boost rate modifier.</summary>
  public float BestBooster { get; private set; }

  // ReSharper restore InconsistentNaming
  // ReSharper restore MemberCanBePrivate.Global
  #endregion

  #region API methods

  /// <summary>Records the owner's request for the growth modification.</summary>
  /// <remarks>The final rate applied may be different if there are more than one owner registered.</remarks>
  /// <param name="ownerId">A unique ID of the caller that applies the modifier.</param>
  /// <param name="modifier">
  /// The rate modifer as a positive or negative value in percents. E.g. <c>15.5</c> (+15.5%) or <c>-8.5</c> (-8.5%).
  /// </param>
  public void RegisterModifier(string ownerId, float modifier) {
    if (!IsLiveAndGrowing) {
      return;
    }
    if (modifier > 0) {
      _registeredBoosts[ownerId] = modifier;
    } else if (modifier < 0) {
      _registeredModerators[ownerId] = modifier;
    }
    UpdateRate();
  }

  /// <summary>Removes any modifications from the owner.</summary>
  /// <param name="ownerId">
  /// The ID of the owner that applied the modifiers. Only the modifiers applied by this owner will be deleted.
  /// </param>
  public void UnregisterModifier(string ownerId) {
    _registeredBoosts.Remove(ownerId);
    _registeredModerators.Remove(ownerId);
    UpdateRate();
  }

  #endregion

  #region Implementation

  ITimeTriggerFactory _timeTriggerFactory;
  Growable _growable;
  readonly Dictionary<string, float> _registeredBoosts = new();
  readonly Dictionary<string, float> _registeredModerators = new();
  float _originalGrowthTimeInDays;

  /// <summary>It must be public for the injection logic to work.</summary>
  [Inject]
  public void InjectDependencies(ITimeTriggerFactory timeTriggerFactory) {
    _timeTriggerFactory = timeTriggerFactory;
  }

  /// <inheritdoc/>
  public void Awake() {
    _growable = GetComponent<Growable>();
    _originalGrowthTimeInDays = GetComponent<GrowableSpec>().GrowthTimeInDays;
  }

  /// <summary>Calculates the effective multiplier and updates the growable settings.</summary>
  /// <seealso cref="EffectiveModifier"/>
  void UpdateRate() {
    BestBooster = _registeredBoosts.Count > 0 ? _registeredBoosts.Values.Max() : 0f;
    WorstModerator = _registeredModerators.Count > 0 ? _registeredModerators.Values.Min() : 0f;
    EffectiveModifier = BestBooster + WorstModerator;
    if (!IsLiveAndGrowing) {
      return; // The growable is in the invalid state.
    }
    var newGrowthTime = _originalGrowthTimeInDays / (1f + EffectiveModifier / 100f);
    var progressDone = _growable.GrowthProgress;
    var newTrigger = _timeTriggerFactory.Create(() => _growable.Grow(), newGrowthTime);
    newTrigger.FastForwardProgress(progressDone);
    _growable._timeTrigger.Reset();
    _growable._timeTrigger = newTrigger;
    _growable._timeTrigger.Resume();
    RateIsModified = Mathf.Abs(_originalGrowthTimeInDays - newGrowthTime) > float.Epsilon;
    _growable._growableSpec = _growable._growableSpec with { GrowthTimeInDays = newGrowthTime };
  }  

  #endregion
}
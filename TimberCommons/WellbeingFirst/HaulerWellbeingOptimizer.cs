using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.Characters;
using Timberborn.GameDistricts;
using Timberborn.NeedBehaviorSystem;
using Timberborn.NeedSystem;
using Timberborn.WalkingSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.WellbeingFirst {

sealed class HaulerWellbeingOptimizer : BaseComponent {
  
  public Character Character { get; private set; }
  public NeedManager NeedManager { get; private set; }
  public ActionDurationCalculator DurationCalculator { get; private set; }
  public Walker Walker { get; private set; }

  public readonly HashSet<string> CriticalNeedsForRole = new() { "Hunger" };

  public bool NeedsOptimization => !DisableOptimization && (IsBuilder() || IsHauler());

  internal bool DisableOptimization;
  
  void Awake() {
    Character = GetComponentFast<Character>();
    NeedManager = GetComponentFast<NeedManager>();
    DurationCalculator = GetComponentFast<ActionDurationCalculator>();
    Walker = GetComponentFast<Walker>();
  }

  public bool IsHauler() {
    return true;
  }

  public bool IsBuilder() {
    return true;
  }

  public bool IsWalkingToSite() {
    //FIXME?
    return true;
  }

  public bool CanCancelCurrentBehavior() {
    return false;
  }

  public float TimeToDestination() {
    return 0;
  }

  public void CancelCurrentBehavior() {
  }
}

}

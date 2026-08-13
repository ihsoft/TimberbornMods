// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.BaseComponentSystem;
using Timberborn.NeedApplication;
using Timberborn.Persistence;
using Timberborn.TimeSystem;
using Timberborn.WorldPersistence;

namespace IgorZ.TimberCommons.NeedApplierUI;

sealed class WorkshopInjuryStatistics : BaseComponent, IAwakableComponent, IPersistentEntity {
  const string InjuryNeedId = "Injury";

  readonly IDayNightCycle _dayNightCycle;

  WorkshopRandomNeedApplier _needApplier;
  DailyInjuryCounter _counter;

  WorkshopInjuryStatistics(IDayNightCycle dayNightCycle) {
    _dayNightCycle = dayNightCycle;
  }

  public IReadOnlyList<int> InjuryHistory {
    get {
      AdvanceDay();
      return _counter.InjuryHistory;
    }
  }

  public int InjuriesYesterday => InjuryHistory.LastOrDefault();
  public int InjuriesInLastWeek => InjuryHistory.TakeLast(7).Sum();
  public int InjuriesToday {
    get {
      AdvanceDay();
      return _counter.InjuriesToday;
    }
  }

  /// <inheritdoc/>
  public void Awake() {
    _counter = new DailyInjuryCounter(_dayNightCycle.DayNumber);
    _needApplier = GetComponent<WorkshopRandomNeedApplier>();
    _needApplier.NeedApplied += OnNeedApplied;
  }

  void OnNeedApplied(object sender, NeedAppliedEventArgs args) {
    if (args.NeedEffect.NeedId != InjuryNeedId) {
      return;
    }
    _counter.RecordInjury(_dayNightCycle.DayNumber);
  }

  void AdvanceDay() {
    _counter.AdvanceDay(_dayNightCycle.DayNumber);
  }

  #region IPersistentEntity implementation

  static readonly ComponentKey ComponentKey = new(typeof(WorkshopInjuryStatistics).FullName);
  static readonly PropertyKey<int> TrackedDayKey = new("TrackedDay");
  static readonly PropertyKey<int> InjuriesTodayKey = new("InjuriesToday");
  static readonly PropertyKey<int> InjuriesYesterdayKey = new("InjuriesYesterday");
  static readonly ListKey<int> InjuryHistoryKey = new("InjuryHistory");

  /// <inheritdoc/>
  public void Save(IEntitySaver entitySaver) {
    AdvanceDay();
    var component = entitySaver.GetComponent(ComponentKey);
    component.Set(TrackedDayKey, _counter.TrackedDay);
    component.Set(InjuriesTodayKey, _counter.InjuriesToday);
    component.Set(InjuryHistoryKey, _counter.InjuryHistory.ToList());
  }

  /// <inheritdoc/>
  public void Load(IEntityLoader entityLoader) {
    if (!entityLoader.TryGetComponent(ComponentKey, out var component)) {
      return;
    }
    var injuryHistory = component.Has(InjuryHistoryKey)
        ? component.Get(InjuryHistoryKey)
        : new List<int> { component.Get(InjuriesYesterdayKey) };
    _counter.Restore(component.Get(TrackedDayKey), component.Get(InjuriesTodayKey), injuryHistory);
    AdvanceDay();
  }

  #endregion
}

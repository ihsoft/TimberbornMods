// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;

namespace IgorZ.TimberCommons.NeedApplierUI;

sealed class DailyInjuryCounter {
  public const int HistoryDays = 28;

  readonly Queue<int> _injuryHistory = new();

  public int TrackedDay { get; private set; }
  public int InjuriesToday { get; private set; }
  public IReadOnlyList<int> InjuryHistory => _injuryHistory.ToArray();
  public int InjuriesYesterday => _injuryHistory.LastOrDefault();
  public int InjuriesInHistory => _injuryHistory.Sum();

  public DailyInjuryCounter(int currentDay) {
    TrackedDay = currentDay;
  }

  public void RecordInjury(int currentDay) {
    AdvanceDay(currentDay);
    InjuriesToday++;
  }

  public void AdvanceDay(int currentDay) {
    if (currentDay <= TrackedDay) {
      return;
    }
    AddHistoryValue(InjuriesToday);
    for (var day = TrackedDay + 1; day < currentDay; day++) {
      AddHistoryValue(0);
    }
    InjuriesToday = 0;
    TrackedDay = currentDay;
  }

  public void Restore(int trackedDay, int injuriesToday, IEnumerable<int> injuryHistory) {
    TrackedDay = trackedDay;
    InjuriesToday = injuriesToday;
    _injuryHistory.Clear();
    foreach (var injuries in injuryHistory.TakeLast(HistoryDays)) {
      AddHistoryValue(injuries);
    }
  }

  void AddHistoryValue(int injuries) {
    if (_injuryHistory.Count == HistoryDays) {
      _injuryHistory.Dequeue();
    }
    _injuryHistory.Enqueue(injuries);
  }
}

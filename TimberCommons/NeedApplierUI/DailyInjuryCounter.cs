// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.TimberCommons.NeedApplierUI;

sealed class DailyInjuryCounter {
  public int TrackedDay { get; private set; }
  public int InjuriesToday { get; private set; }
  public int InjuriesYesterday { get; private set; }

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
    InjuriesYesterday = currentDay == TrackedDay + 1 ? InjuriesToday : 0;
    InjuriesToday = 0;
    TrackedDay = currentDay;
  }

  public void Restore(int trackedDay, int injuriesToday, int injuriesYesterday) {
    TrackedDay = trackedDay;
    InjuriesToday = injuriesToday;
    InjuriesYesterday = injuriesYesterday;
  }
}

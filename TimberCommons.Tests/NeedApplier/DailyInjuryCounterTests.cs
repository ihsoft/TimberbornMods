// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.NeedApplier;

namespace TimberCommons.Tests;

static class DailyInjuryCounterTests {
  public static void MovesCurrentInjuriesToYesterdayOnNextDay() {
    var counter = new DailyInjuryCounter(10);

    counter.RecordInjury(10);
    counter.RecordInjury(10);
    counter.AdvanceDay(11);

    Assert.Equal(2, counter.InjuriesYesterday);
    Assert.Equal(0, counter.InjuriesToday);
  }

  public static void ClearsYesterdayAfterSkippedDay() {
    var counter = new DailyInjuryCounter(10);

    counter.RecordInjury(10);
    counter.AdvanceDay(12);

    Assert.Equal(0, counter.InjuriesYesterday);
    Assert.Equal(0, counter.InjuriesToday);
  }

  public static void RestoresSavedCountsAndAdvancesThem() {
    var counter = new DailyInjuryCounter(12);

    counter.Restore(trackedDay: 10, injuriesToday: 3, injuryHistory: new[] { 1 });
    counter.AdvanceDay(11);

    Assert.Equal(3, counter.InjuriesYesterday);
    Assert.Equal(0, counter.InjuriesToday);
  }

  public static void KeepsLatestTwentyEightCompletedDays() {
    var counter = new DailyInjuryCounter(0);

    for (var day = 0; day < 30; day++) {
      for (var injury = 0; injury < day; injury++) {
        counter.RecordInjury(day);
      }
      counter.AdvanceDay(day + 1);
    }

    Assert.Equal(DailyInjuryCounter.HistoryDays, counter.InjuryHistory.Count);
    Assert.Equal(2, counter.InjuryHistory[0]);
    Assert.Equal(29, counter.InjuriesYesterday);
  }
}

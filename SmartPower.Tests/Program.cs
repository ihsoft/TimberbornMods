using System;
using System.Collections.Generic;

namespace SmartPower.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("SmartPowerService tracks ticks and startup delay", SmartPowerServiceTests.TracksTicksAndStartupDelay),
      ("SmartPowerService reports paused game state", SmartPowerServiceTests.ReportsPausedGameState),
      ("SmartPowerService caches fixed delta time in minutes", SmartPowerServiceTests.CachesFixedDeltaTimeInMinutes),
      ("SmartPowerService creates tick delayed actions bound to current tick",
          SmartPowerServiceTests.CreatesTickDelayedActions),
      ("SmartPowerService creates time delayed actions from fixed delta time",
          SmartPowerServiceTests.CreatesTimeDelayedActions),
      ("SmartPowerService sums reserved power by graph", SmartPowerServiceTests.SumsReservedPowerByGraph),
      ("SmartPowerService updates existing power reservations", SmartPowerServiceTests.UpdatesReservations),
      ("SmartPowerService removes power reservations", SmartPowerServiceTests.RemovesReservations),
      ("SmartPowerService accepts reservations before graph initialization",
          SmartPowerServiceTests.AcceptsReservationsBeforeGraphInitialization),
  ];

  static int Main() {
    var failed = 0;
    foreach (var (name, test) in Tests) {
      try {
        test();
        Console.WriteLine("[PASS] " + name);
      } catch (Exception e) {
        failed++;
        Console.WriteLine("[FAIL] " + name);
        Console.WriteLine(e);
      }
    }

    Console.WriteLine();
    Console.WriteLine($"Total: {Tests.Count}, Passed: {Tests.Count - failed}, Failed: {failed}");
    return failed == 0 ? 0 : 1;
  }
}

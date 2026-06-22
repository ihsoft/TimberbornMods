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
      ("PowerOutputBalancer suspends when batteries are charged", PowerOutputBalancerTests.SuspendsChargedBatteries),
      ("PowerOutputBalancer resumes when batteries discharge", PowerOutputBalancerTests.ResumesDischargedBatteries),
      ("PowerOutputBalancer uses reserved power when deciding to resume",
          PowerOutputBalancerTests.ResumesForReservedPowerDemand),
      ("PowerOutputBalancer suspends immediately when inactive output is redundant",
          PowerOutputBalancerTests.SuspendsInactiveRedundantOutput),
      ("PowerOutputBalancer keeps running when its output is needed", PowerOutputBalancerTests.KeepsNeededOutputRunning),
      ("PowerOutputBalancer resumes when automation is disabled", PowerOutputBalancerTests.ResumesWhenAutomationDisabled),
      ("PowerOutputBalancer skips balancing when disabled", PowerOutputBalancerTests.SkipsBalancingWhenDisabled),
      ("PowerInputLimiter suspends immediately on low batteries", PowerInputLimiterTests.SuspendsOnLowBatteries),
      ("PowerInputLimiter resumes when automation is disabled", PowerInputLimiterTests.ResumesWhenAutomationDisabled),
      ("PowerInputLimiter suspends when no batteries and efficiency is low",
          PowerInputLimiterTests.SuspendsOnLowEfficiencyWithoutBatteries),
      ("PowerInputLimiter resumes when supply can cover desired power", PowerInputLimiterTests.ResumesWhenSupplyRecovers),
      ("PowerInputLimiter updates adjustable power input", PowerInputLimiterTests.UpdatesAdjustablePowerInput),
      ("SmartPoweredAttraction lowers power when empty", SmartPoweredAttractionTests.LowersPowerWhenEmpty),
      ("SmartPoweredAttraction uses nominal power when occupied", SmartPoweredAttractionTests.UsesNominalPowerWhenOccupied),
      ("SmartPoweredAttraction returns zero when inactive", SmartPoweredAttractionTests.ReturnsZeroWhenInactive),
      ("SmartManufactory enters standby when ingredients are missing", SmartManufactoryTests.StandbyOnMissingIngredients),
      ("SmartManufactory uses nominal power when recipe can run", SmartManufactoryTests.NominalPowerWhenReady),
      ("SmartManufactory returns zero without current recipe", SmartManufactoryTests.ZeroWithoutRecipe),
  ];

  static int Main() {
    return TestRunner.Run(Tests);
  }
}

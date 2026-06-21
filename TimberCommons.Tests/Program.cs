using System;
using System.Collections.Generic;

namespace TimberCommons.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("GrowthRateModifier combines strongest boost and moderator", GrowthRateModifierTests.CombinesModifiers),
      ("GrowthRateModifier recalculates after unregister", GrowthRateModifierTests.RecalculatesAfterUnregister),
      ("GrowthRateModifier ignores inactive growables", GrowthRateModifierTests.IgnoresInactiveGrowables),
      ("GoodConsumingIrrigationTower scales consumption from coverage",
          GoodConsumingIrrigationTowerTests.ScalesConsumptionFromCoverage),
      ("GoodConsumingIrrigationTower toggles consumption from coverage",
          GoodConsumingIrrigationTowerTests.TogglesConsumptionFromCoverage),
      ("GoodConsumingIrrigationTower moisturizes only when consumption can run",
          GoodConsumingIrrigationTowerTests.MoisturizesOnlyWhenConsumptionCanRun),
      ("GoodConsumingIrrigationTower multiplies finished-building efficiency providers",
          GoodConsumingIrrigationTowerTests.MultipliesFinishedBuildingEfficiencyProviders),
      ("ManufactoryIrrigationTower scales recipe duration from coverage",
          ManufactoryIrrigationTowerTests.ScalesRecipeDurationFromCoverage),
      ("ManufactoryIrrigationTower reports no tiles instead of supply time at zero coverage",
          ManufactoryIrrigationTowerTests.ReportsNoTilesAtZeroCoverage),
      ("ManufactoryIrrigationTower moisturizes only when unblocked and ready",
          ManufactoryIrrigationTowerTests.MoisturizesOnlyWhenUnblockedAndReady),
      ("ManufactoryIrrigationTower reports production efficiency only when ready",
          ManufactoryIrrigationTowerTests.ReportsProductionEfficiencyOnlyWhenReady),
      ("BlockContaminationRangeEffect applies and replaces contamination override",
          BlockContaminationRangeEffectTests.AppliesAndReplacesContaminationOverride),
      ("BlockContaminationRangeEffect saves and claims loaded override",
          BlockContaminationRangeEffectTests.SavesAndClaimsLoadedOverride),
      ("ModifyGrowableGrowthRangeEffect applies and resets matching growables",
          ModifyGrowableGrowthRangeEffectTests.AppliesAndResetsMatchingGrowables),
      ("ModifyGrowableGrowthRangeEffect honors template and component filters",
          ModifyGrowableGrowthRangeEffectTests.HonorsTemplateAndComponentFilters),
      ("ModifyGrowableGrowthRangeEffect handles new initialized entities",
          ModifyGrowableGrowthRangeEffectTests.HandlesNewInitializedEntities),
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

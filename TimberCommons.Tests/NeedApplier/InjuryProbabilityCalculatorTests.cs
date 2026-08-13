using IgorZ.TimberCommons.NeedApplier;

namespace TimberCommons.Tests;

static class InjuryProbabilityCalculatorTests {
  public static void CalculatesProbabilityOfAtLeastOneDailyInjury() {
    Assert.Equal(0.214321f, InjuryProbabilityCalculator.CalculateDailyProbability(0.01f), tolerance: 0.000001f);
  }
}

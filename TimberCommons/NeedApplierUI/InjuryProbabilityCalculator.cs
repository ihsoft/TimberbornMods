// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.TimberCommons.NeedApplierUI;

static class InjuryProbabilityCalculator {
  public static float CalculateDailyProbability(float hourlyProbability) {
    return 1f - MathF.Pow(1f - hourlyProbability, 24);
  }
}

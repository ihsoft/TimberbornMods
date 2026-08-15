// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;

namespace IgorZ.FillTheGap.Tests;

static class FillTheGapTestSuite {
  static readonly List<(string Name, Action Test)> Tests = [
      ("Folktails Platforms shorten through every supported height", PlatformReplacementRulesTests.MapsFolktails),
      ("Iron Teeth Platforms shorten through every supported height", PlatformReplacementRulesTests.MapsIronTeeth),
      ("Unknown templates are not treated as supported Platforms",
          PlatformReplacementRulesTests.RejectsUnknownTemplate),
      ("Platform volume contains exactly its occupied levels", PlatformReplacementRulesTests.ContainsOccupiedLevels),
      ("Completed Platform support exists only above its top level", PlatformReplacementRulesTests.FindsTopLevel),
  ];

  public static int Run() {
    return TestRunner.Run(Tests);
  }
}

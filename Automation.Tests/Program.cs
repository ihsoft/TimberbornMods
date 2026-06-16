using System;
using System.Collections.Generic;

namespace Automation.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("AutomationBehavior creates dynamic component only once", AutomationBehaviorTests.GetOrCreateCachesComponent),
      ("AutomationBehavior reports missing dynamic component", AutomationBehaviorTests.GetOrThrowReportsMissingComponent),
      ("AutomationBehavior creates component with Awake callback", AutomationBehaviorTests.GetOrCreateCallsAwake),
      ("AutomationBehavior replays finished callback after late creation", AutomationBehaviorTests.GetOrCreateAfterFinished),
      ("AutomationBehavior replays initialized callback after late creation", AutomationBehaviorTests.GetOrCreateAfterInitialized),
      ("AutomationBehavior replays finished and initialized callbacks in order",
          AutomationBehaviorTests.GetOrCreateAfterFinishedAndInitialized),
      ("AutomationBehavior forwards lifecycle callbacks to existing dynamic components",
          AutomationBehaviorTests.ForwardsLifecycleCallbacks),
      ("AutomationBehavior delete forwards to dynamic components", AutomationBehaviorTests.DeleteEntityForwardsToComponents),
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

using System;
using System.Collections.Generic;

static class TestRunner {
  public static int Run(IReadOnlyList<(string Name, Action Test)> tests) {
    var failed = 0;
    foreach (var (name, test) in tests) {
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
    Console.WriteLine($"Total: {tests.Count}, Passed: {tests.Count - failed}, Failed: {failed}");
    return failed == 0 ? 0 : 1;
  }
}

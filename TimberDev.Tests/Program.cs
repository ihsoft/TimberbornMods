using System;
using System.Collections.Generic;
using Timberborn.Localization;

namespace TimberDev.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("CommonFormats.FormatSmallValue keeps compact precision", CommonFormatsTests.FormatSmallValue),
      ("CommonFormats.DaysHoursFormat formats boundary values", CommonFormatsTests.DaysHoursFormat),
      ("CommonFormats.FormatSupplyLeft caches and resets localized template", CommonFormatsTests.FormatSupplyLeft),
      ("CommonFormats highlight helpers wrap text with color tags", CommonFormatsTests.Highlight),
      ("UnitFormats delegates to localization keys", UnitFormatsTests.DelegatesToLocalizationKeys),
      ("FeatureController reads feature flags and values", FeatureControllerTests.ReadFeatures),
      ("FeatureController rejects invalid feature names", FeatureControllerTests.RejectsInvalidNames),
      ("FeatureController validates flags and values", FeatureControllerTests.ValidatesHelpers),
      ("TickDelayedAction executes after sequential ticks", TickDelayedActionTests.ExecutesAfterSequentialTicks),
      ("TickDelayedAction restarts after skipped tick", TickDelayedActionTests.RestartsAfterSkippedTick),
      ("TickDelayedAction force executes immediately", TickDelayedActionTests.ForceExecutesImmediately),
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

sealed class FakeLoc : ILoc {
  readonly Dictionary<string, string> _templates = new();

  public readonly List<(string Key, string Value)> Calls = new();

  public void Set(string key, string template) {
    _templates[key] = template;
  }

  public string T(string key, params object[] args) {
    var value = args.Length > 0 ? args[0]?.ToString() : "";
    Calls.Add((key, value));
    return _templates.TryGetValue(key, out var template) ? string.Format(template, args) : $"{key}:{value}";
  }
}

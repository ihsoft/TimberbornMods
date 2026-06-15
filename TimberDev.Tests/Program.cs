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
      ("DropdownItem converts from tuples", DropdownItemTests.ConvertsFromTuples),
      ("VisualElementExtensions finds or reports", VisualElementExtensionsTests.FindsElementOrReportsMissingElement),
      ("VisualEffects schedules switch effect", VisualEffectsTests.SchedulesSwitchEffect),
      ("VisualEffects temporarily sets class", VisualEffectsTests.TemporarilySetsClass),
      ("PanelFragmentPatcher inserts once", PanelFragmentPatcherTests.InsertsElementAfterTargetOnlyOnce),
      ("PanelFragmentPatcher appends without target", PanelFragmentPatcherTests.AppendsElementWhenTargetIsMissing),
      ("PreciseSliderWrapper rounds value", PreciseSliderWrapperTests.RoundsChangedValueToStepAndInvokesCallback),
      ("PreciseSliderWrapper updates without callback", PreciseSliderWrapperTests.UpdatesValuesWithoutCallback),
      ("ResizableDropdownElement selects item", ResizableDropdownElementTests.SelectsFirstItemAndUpdatesValue),
      ("ResizableDropdownElement resizes width", ResizableDropdownElementTests.AutoResizeCanBeDisabled),
      ("UiFactory localizes and caches stylesheet", UiFactoryTests.DelegatesLocalizationAndCachesStylesheet),
      ("UiFactory creates initialized controls", UiFactoryTests.CreatesInitializedControls),
      ("UiFactory slider helpers round values", UiFactoryTests.SliderHelpersRoundValues),
      ("UiFactory creates dropdown and finds upstream", UiFactoryTests.CreatesSimpleDropdownAndFindsUpstreamElement),
      ("CounterProfiler reports sorted frame stats and resets", CounterProfilerTests.ReportsSortedFrameStatsAndResets),
      ("PausedStopwatch pauses and resumes", PausedStopwatchTests.PausesRunningStopwatchAndResumesOnDispose),
      ("PausedStopwatch starts stopped stopwatch on dispose", PausedStopwatchTests.StartsStoppedStopwatchOnDispose),
      ("TicksProfiler reports hits and samples and resets", TicksProfilerTests.ReportsHitsAndSamplesAndResets),
      ("TimedUpdater executes only after threshold", TimedUpdaterTests.ExecutesOnlyAfterThreshold),
      ("TimedUpdater startNow and force control execution", TimedUpdaterTests.StartNowAndForceControlExecution),
      ("ReflectionsHelper creates compatible instance", ReflectionsHelperTests.GetsCompatibleTypeAndCreatesInstance),
      ("ReflectionsHelper handles missing and invalid types", ReflectionsHelperTests.HandlesMissingAndInvalidTypes),
      ("IObjectLoaderExtensions returns defaults", IObjectLoaderExtensionsTests.ReturnsDefaultsForMissingValues),
      ("IObjectLoaderExtensions returns stored values", IObjectLoaderExtensionsTests.ReturnsStoredValues),
      ("ComponentsAccessor returns goods inventory", ComponentsAccessorTests.ReturnsFirstNonConstructionInventory),
      ("ComponentsAccessor handles missing inventory", ComponentsAccessorTests.HandlesMissingInventory),
      ("ModTextAssetConverterPatch appends path", ModTextAssetConverterPatchTests.AppendsSourcePathForValidExtension),
      ("ModTextAssetConverterPatch ignores invalid extension", ModTextAssetConverterPatchTests.IgnoresInvalidExtension),
      ("StaticBindings stores dependency container", StaticBindingsTests.ConstructorStoresDependencyContainer),
      ("StaticBindingsConfigurator registers singleton", StaticBindingsTests.ConfiguratorRegistersSingleton),
      ("StaticBindingsConfigurator declares contexts", StaticBindingsTests.ConfiguratorDeclaresExpectedContexts),
      ("HarmonyPatcher applies once", HarmonyPatcherTests.ApplyPatchRegistersAndRejectsDuplicateId),
      ("HarmonyPatcher unpatcher removes patches", HarmonyPatcherTests.UnpatcherRemovesPatchesInReverseOrder),
      ("HarmonyPatcherConfigurator registers singleton", HarmonyPatcherTests.ConfiguratorRegistersUnpatcherAsSingleton),
      ("HarmonyPatcherConfigurator declares contexts", HarmonyPatcherTests.ConfiguratorDeclaresAllContexts),
      ("CustomizableInstantiator replaces patcher", CustomizableInstantiatorTests.AddPatcherReplacesById),
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

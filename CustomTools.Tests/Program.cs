using System;
using System.Collections.Generic;

namespace CustomTools.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("FeatureLimiterService allows tools without limiter", FeatureLimiterServiceTests.AllowsToolsWithoutLimiter),
      ("FeatureLimiterService allows empty faction filters", FeatureLimiterServiceTests.AllowsEmptyFactionFilters),
      ("FeatureLimiterService allows current faction", FeatureLimiterServiceTests.AllowsCurrentFaction),
      ("FeatureLimiterService rejects different factions", FeatureLimiterServiceTests.RejectsDifferentFaction),
      ("CustomToolsUndoService registers on post load", CustomToolsUndoServiceTests.RegistersOnPostLoad),
      ("CustomToolsUndoService commits and undoes captures", CustomToolsUndoServiceTests.CommitsAndUndoesCaptures),
      ("CustomToolsUndoService aborts captures", CustomToolsUndoServiceTests.AbortsCaptures),
      ("CustomToolsUndoService clears pending undo actions", CustomToolsUndoServiceTests.ClearsUndoActions),
      ("CustomToolsUndoService ignores creation outside capture",
          CustomToolsUndoServiceTests.IgnoresCreationOutsideCapture),
      ("CustomToolsUndoService keeps nested capture as one action",
          CustomToolsUndoServiceTests.KeepsNestedCaptureAsOneAction),
      ("KeyBindingInputProcessor registers itself and clears state on post load",
          KeyBindingInputProcessorTests.RegistersItselfAndClearsStateOnPostLoad),
      ("KeyBindingInputProcessor selects block object tool and blocks consumed key",
          KeyBindingInputProcessorTests.SelectsBlockObjectToolAndBlocksConsumedKey),
      ("KeyBindingInputProcessor selects tool by type", KeyBindingInputProcessorTests.SelectsToolByType),
      ("KeyBindingInputProcessor selects tool by custom tool id",
          KeyBindingInputProcessorTests.SelectsToolByCustomToolId),
      ("KeyBindingInputProcessor processes newest pressed binding first",
          KeyBindingInputProcessorTests.ProcessesNewestPressedBindingFirst),
      ("KeyBindingInputProcessor posts event for unrouted binding",
          KeyBindingInputProcessorTests.PostsEventForUnroutedBinding),
      ("KeyBindingInputProcessor ignores bindings without custom tool spec",
          KeyBindingInputProcessorTests.IgnoresBindingsWithoutCustomToolSpec),
      ("KeyBindingInputProcessor throws when block object blueprint is unknown",
          KeyBindingInputProcessorTests.ThrowsWhenBlockObjectBlueprintIsUnknown),
  ];

  static int Main() {
    return TestRunner.Run(Tests);
  }
}

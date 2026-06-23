using System;
using System.Collections.Generic;

namespace XRay.Tests;

static class Program {
  static readonly List<(string Name, Action Test)> Tests = [
      ("XRayModeManager toggles services once per state transition", XRayModeManagerTests.TogglesServices),
      ("XRayModeManager skips duplicate state changes", XRayModeManagerTests.SkipsDuplicateStateChanges),
      ("KeyBindingInputProcessor registers itself", KeyBindingInputProcessorTests.RegistersItself),
      ("KeyBindingInputProcessor activates while show binding is held", KeyBindingInputProcessorTests.ActivatesOnHold),
      ("KeyBindingInputProcessor ignores hold while toggle mode is active",
          KeyBindingInputProcessorTests.IgnoresHoldWhenActive),
  ];

  static int Main() {
    return TestRunner.Run(Tests);
  }
}

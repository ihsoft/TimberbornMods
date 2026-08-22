using IgorZ.Automation.ScriptingEngineUI;

namespace Automation.Tests;

static class PinnedSignalNamesTests {
  const string SignalName = "Signals.Floodgate.Height";
  const string DefaultName = "Floodgate.Height";

  public static void PreservesUnrestrictedNamesAndTrimsEdges() {
    var names = new PinnedSignalNames();

    names.Set(SignalName, "  Верхний шлюз №1 / open!?  ");

    Assert.Equal("Верхний шлюз №1 / open!?", names.GetDisplayName(SignalName, DefaultName));
    Assert.Equal("Верхний шлюз №1 / open!?", names.GetSavedName(SignalName));
  }

  public static void EmptyNameRestoresDefaultName() {
    var names = new PinnedSignalNames();
    names.Set(SignalName, "Custom name");

    names.Set(SignalName, "   ");

    Assert.Equal(DefaultName, names.GetDisplayName(SignalName, DefaultName));
    Assert.Equal("", names.GetSavedName(SignalName));
  }

  public static void RemovingSignalDropsPinnedName() {
    var names = new PinnedSignalNames();
    names.Set(SignalName, "Custom name");

    names.Remove(SignalName);

    Assert.Equal(DefaultName, names.GetDisplayName(SignalName, DefaultName));
    Assert.Equal("", names.GetSavedName(SignalName));
  }
}

using IgorZ.Automation.ScriptingEngine.Core;

namespace Automation.Tests;

static class SignalModuleOrderTests {
  public static void PreservesRegistrationOrderByDefault() {
    SignalModuleOrder.Load("");

    var result = SignalModuleOrder.Apply(["Inventory", "Power", "Time"], moduleName => moduleName);

    Assert.Equal("Inventory|Power|Time", string.Join("|", result));
  }

  public static void AppliesCustomOrderAndAppendsUnknownModules() {
    SignalModuleOrder.Load("Power|Inventory");

    try {
      var result = SignalModuleOrder.Apply(["Inventory", "NewModule", "Power", "Time"], moduleName => moduleName);

      Assert.Equal("Power|Inventory|NewModule|Time", string.Join("|", result));
    } finally {
      SignalModuleOrder.Load("");
    }
  }

  public static void SerializesEachModuleOnce() {
    var result = SignalModuleOrder.Serialize(["Power", "Inventory", "Power"]);

    Assert.Equal("Power|Inventory", result);
  }
}

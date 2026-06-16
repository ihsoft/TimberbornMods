using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;

namespace Automation.Tests;

static class AutomationExtensionsRegistryTests {
  public static void GetsRegisteredExtensionByNameAndType() {
    var registry = new AutomationExtensionsRegistry();
    var extension = new TestWeatherExtension();

    registry.RegisterExtension(nameof(IWeatherExtension), extension);

    Assert.Same(extension, registry.GetExtension(nameof(IWeatherExtension)));
    Assert.Same(extension, registry.GetExtension<IWeatherExtension>());
  }

  public static void IgnoresDuplicateExtensionRegistration() {
    var registry = new AutomationExtensionsRegistry();
    var firstExtension = new TestWeatherExtension();
    var secondExtension = new TestWeatherExtension();

    registry.RegisterExtension(nameof(IWeatherExtension), firstExtension);
    registry.RegisterExtension(nameof(IWeatherExtension), secondExtension);

    Assert.Same(firstExtension, registry.GetExtension<IWeatherExtension>());
  }

  public static void ReturnsNullForMissingExtension() {
    var registry = new AutomationExtensionsRegistry();

    Assert.Equal(null, registry.GetExtension(nameof(IWeatherExtension)));
    Assert.Equal(null, registry.GetExtension<IWeatherExtension>());
  }

  sealed class TestWeatherExtension : IWeatherExtension {
    public void AddWeatherId(string weatherId, string nameLocKey) {
    }

    public void AddTemperateWeatherIdProvider(System.Func<string> getCurrentWeatherIdFunc) {
    }

    public void TriggerSeasonCheck() {
    }
  }
}

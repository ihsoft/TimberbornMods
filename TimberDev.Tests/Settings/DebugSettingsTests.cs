using IgorZ.TimberDev.Settings;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityDev.Utils.LogUtilsLite;

namespace TimberDev.Tests;

static class DebugSettingsTests {
  public static void ExposesSettingsMetadata() {
    var settings = new TestDebugSettings();

    Assert.Equal("TimberDev_Utils.Settings.DebugSection", settings.HeaderLocKey);
    Assert.Equal(100, settings.Order);
    Assert.Equal(ModSettingsContext.MainMenu | ModSettingsContext.Game, settings.ChangeableOn);
    Assert.Equal("TimberDev_Utils.Settings.Debug.VerboseLogging", settings.VerboseLogging.Descriptor.LocKey);
    Assert.Equal(
        "TimberDev_Utils.Settings.Debug.VerboseLoggingTooltip",
        settings.VerboseLogging.Descriptor.TooltipLocKey);
  }

  public static void UpdatesVerbosityOnLoadAndValueChange() {
    var settings = new TestDebugSettings();
    DebugEx.VerbosityLevel = DebugEx.LogLevel.Finer;

    settings.TriggerAfterLoad();
    Assert.Equal(DebugEx.LogLevel.Info, DebugEx.VerbosityLevel);

    settings.VerboseLogging.SetValue(true);
    Assert.Equal(DebugEx.LogLevel.Finer, DebugEx.VerbosityLevel);

    settings.VerboseLogging.SetValue(false);
    Assert.Equal(DebugEx.LogLevel.Info, DebugEx.VerbosityLevel);
  }

  sealed class TestDebugSettings : DebugSettings<TestDebugSettings> {
    protected override string ModId => "Test.Mod";

    public TestDebugSettings()
        : base(new TestSettings(), new ModSettingsOwnerRegistry(), new ModRepository()) {
    }

    public void TriggerAfterLoad() {
      OnAfterLoad();
    }
  }
}

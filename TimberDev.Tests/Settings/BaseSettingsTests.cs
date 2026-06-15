using IgorZ.TimberDev.Settings;
using ModSettings.Common;
using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;
using UnityEngine;

namespace TimberDev.Tests;

static class BaseSettingsTests {
  public static void RunsCallbacksOnLoadAndValueChange() {
    var settings = new TestBaseSettings();

    settings.NumberSetting.SetValue(7);
    settings.ColorSetting.SetColor(new Color(0.1f, 0.2f, 0.3f));
    Assert.Equal(7, settings.NumberValue);
    Assert.Equal(1, settings.ColorActionCount);

    settings.NumberSetting.SetValue(11);
    settings.ColorSetting.SetColor(new Color(0.4f, 0.5f, 0.6f));
    settings.TriggerAfterLoad();

    Assert.Equal(11, settings.NumberValue);
    Assert.Equal(new Color(0.4f, 0.5f, 0.6f), settings.ColorValue);
    Assert.Equal(3, settings.ColorActionCount);

    settings.NumberSetting.SetValue(13);
    settings.TriggerAfterLoad();

    Assert.Equal(13, settings.NumberValue);
    Assert.Equal(3, settings.ColorActionCount);
  }

  sealed class TestBaseSettings : BaseSettings<TestBaseSettings> {
    public readonly ModSetting<int> NumberSetting = new(1, ModSettingDescriptor.CreateLocalized("Number"));
    public readonly ColorModSetting ColorSetting = new(new Color(1f, 1f, 1f));

    public int NumberValue { get; private set; }
    public Color ColorValue { get; private set; }
    public int ColorActionCount { get; private set; }

    protected override string ModId => "Test.Mod";
    public override string HeaderLocKey => "Header";
    public override int Order => 1;
    public override ModSettingsContext ChangeableOn => ModSettingsContext.Game;

    public TestBaseSettings()
        : base(new TestSettings(), new ModSettingsOwnerRegistry(), new ModRepository()) {
      InstallSettingCallback(NumberSetting, value => NumberValue = value);
      InstallSettingCallback(ColorSetting, value => ColorValue = value);
      InstallSettingCallback(ColorSetting, () => ColorActionCount++);
    }

    public void TriggerAfterLoad() {
      OnAfterLoad();
    }
  }
}

using System;
using System.Reflection;
using IgorZ.TimberDev.UI;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using UnityEngine.UIElements;

namespace TimberDev.Tests;

static class UiFactoryTests {
  public static void DelegatesLocalizationAndCachesStylesheet() {
    var loc = new FakeLoc();
    loc.Set("caption", "Caption {0}");
    var assetLoader = new TestAssetLoader();
    var factory = CreateFactory(loc: loc, assetLoader: assetLoader);

    Assert.Equal("Caption value", factory.T("caption", "value"));
    Assert.Equal(factory.TimberDevStylesheet, factory.TimberDevStylesheet);
    Assert.Equal(1, assetLoader.Paths.Count);

    factory.TimberDevStylesheetPath = "Other/Style";
    _ = factory.TimberDevStylesheet;

    Assert.Equal(2, assetLoader.Paths.Count);
    Assert.Equal("Other/Style", assetLoader.Paths[1]);
  }

  public static void CreatesInitializedControls() {
    var loc = new FakeLoc();
    loc.Set("button", "Button");
    var initializer = new VisualElementInitializer();
    var factory = CreateFactory(loc: loc, initializer: initializer);
    var clicked = 0;

    var panel = factory.CreateCenteredPanelFragment();
    var label = factory.CreateLabel("label");
    var button = factory.CreateButton("button", _ => clicked++);
    var textField = factory.CreateTextField(120);

    button.Click();

    Assert.True(panel.ClassListContains("initialized"));
    Assert.True(label.ClassListContains(UiFactory.EntityPanelTextClass));
    Assert.Equal("Button", button.text);
    Assert.Equal(1, clicked);
    Assert.Equal(120f, textField.style.width.Value);
    Assert.Equal(4, initializer.Initialized.Count);
  }

  public static void SliderHelpersRoundValues() {
    var factory = CreateFactory();
    var sliderValue = 0f;
    var preciseValue = 0f;

    var slider = factory.CreateSlider(evt => sliderValue = evt.newValue, 0f, 10f, stepSize: 0.5f);
    slider.TriggerValueChanged(0f, 1.26f);

    var preciseSlider = new PreciseSlider();
    factory.AddFixedStepChangeHandler(preciseSlider, 0.25f, value => preciseValue = value);
    preciseSlider.TriggerValueChanged(1.37f);

    Assert.Equal(1.5f, slider.value);
    Assert.Equal(1.5f, sliderValue);
    Assert.Equal(1.25f, preciseSlider.Value);
    Assert.Equal(1.25f, preciseValue);
  }

  public static void CreatesSimpleDropdownAndFindsUpstreamElement() {
    var factory = CreateFactory();
    var selected = "";
    var dropdown = factory.CreateSimpleDropdown(value => selected = value);
    dropdown.Items = [
        ("first", "First"),
        ("second", "Second"),
    ];

    dropdown.SelectedValue = "second";

    Assert.Equal("second", selected);
    Assert.Equal(
        dropdown.Q<Button>("Selection"),
        UiFactory.FindElementUpstream<Button>(dropdown.ChildAt(0), "Selection"));
  }

  static UiFactory CreateFactory(
      FakeLoc loc = null,
      TestAssetLoader assetLoader = null,
      VisualElementInitializer initializer = null) {
    var constructor = typeof(UiFactory).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [
            typeof(VisualElementLoader),
            typeof(Timberborn.Localization.ILoc),
            typeof(IAssetLoader),
            typeof(VisualElementInitializer),
            typeof(DropdownListDrawer),
        ],
        null);
    return (UiFactory)constructor.Invoke([
        new VisualElementLoader(),
        loc ?? new FakeLoc(),
        assetLoader ?? new TestAssetLoader(),
        initializer ?? new VisualElementInitializer(),
        new DropdownListDrawer(),
    ]);
  }
}

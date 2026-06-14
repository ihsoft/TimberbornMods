using IgorZ.TimberDev.UI;
using UnityEngine;

namespace TimberDev.Tests;

static class CommonFormatsTests {
  public static void FormatSmallValue() {
    Assert.Equal("13", CommonFormats.FormatSmallValue(12.6f));
    Assert.Equal("1.3", CommonFormats.FormatSmallValue(1.26f));
    Assert.Equal("0.13", CommonFormats.FormatSmallValue(0.126f));
    Assert.Equal("0.012", CommonFormats.FormatSmallValue(0.01234f));
  }

  public static void DaysHoursFormat() {
    var loc = new FakeLoc();

    Assert.Equal(UnitFormats.HourUnitLocKey + ":0", CommonFormats.DaysHoursFormat(loc, 0f));
    Assert.Equal(UnitFormats.HourUnitLocKey + ":0.5", CommonFormats.DaysHoursFormat(loc, 0.5f));
    Assert.Equal(UnitFormats.HourUnitLocKey + ":1.5", CommonFormats.DaysHoursFormat(loc, 1.51f));
    Assert.Equal(UnitFormats.HourUnitLocKey + ":10", CommonFormats.DaysHoursFormat(loc, 10.4f));
    Assert.Equal(UnitFormats.DayUnitLocKey + ":1", CommonFormats.DaysHoursFormat(loc, 24f));
    Assert.Equal(
        UnitFormats.DayUnitLocKey + ":1 " + UnitFormats.HourUnitLocKey + ":2",
        CommonFormats.DaysHoursFormat(loc, 26.2f));
  }

  public static void FormatSupplyLeft() {
    CommonFormats.ResetCachedLocStrings();

    var loc = new FakeLoc();
    loc.Set("GoodConsuming.SupplyRemaining", "Supply lasts {0}");
    Assert.Equal("Supply lasts " + UnitFormats.HourUnitLocKey + ":3", CommonFormats.FormatSupplyLeft(loc, 3f));

    loc.Set("GoodConsuming.SupplyRemaining", "New template {0}");
    Assert.Equal("Supply lasts " + UnitFormats.HourUnitLocKey + ":4", CommonFormats.FormatSupplyLeft(loc, 4f));

    CommonFormats.ResetCachedLocStrings();
    Assert.Equal("New template " + UnitFormats.HourUnitLocKey + ":5", CommonFormats.FormatSupplyLeft(loc, 5f));
  }

  public static void Highlight() {
    Assert.Equal("<color=#FF4C4C>bad</color>", CommonFormats.HighlightRed("bad"));
    Assert.Equal("<color=#59FF61>good</color>", CommonFormats.HighlightGreen("good"));
    Assert.Equal("<color=#FFFF1A>warn</color>", CommonFormats.HighlightYellow("warn"));
    Assert.Equal("<color=#1A334C>custom</color>", CommonFormats.Highlight("custom", new Color(0.1f, 0.2f, 0.3f)));
    Assert.Equal("<s>done</s>", CommonFormats.Strikethrough("done"));
  }
}

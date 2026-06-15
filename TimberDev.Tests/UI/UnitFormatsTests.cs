using IgorZ.TimberDev.UI;

namespace TimberDev.Tests;

static class UnitFormatsTests {
  public static void DelegatesToLocalizationKeys() {
    var loc = new FakeLoc();

    Assert.Equal(UnitFormats.TickUnitLocKey + ":3", UnitFormats.FormatTicks(3, loc));
    Assert.Equal(UnitFormats.HourUnitLocKey + ":2.5", UnitFormats.FormatHours("2.5", loc));
    Assert.Equal(UnitFormats.DayUnitLocKey + ":7", UnitFormats.FormatDays("7", loc));
    Assert.Equal(UnitFormats.FlowUnitLocKey + ":1.23", UnitFormats.FormatFlow(1.234f, loc));
    Assert.Equal(UnitFormats.DistanceUnitLocKey + ":2.35", UnitFormats.FormatDistance(2.345f, loc));
    Assert.Equal(UnitFormats.AngleUnitLocKey + ":45", UnitFormats.FormatAngle(45, loc));
    Assert.Equal(UnitFormats.PowerUnitLocKey + ":120", UnitFormats.FormatPower(120, loc));
    Assert.Equal(UnitFormats.PowerCapacityUnitLocKey + ":300", UnitFormats.FormatPowerCapacity(300, loc));
    Assert.Equal(
        UnitFormats.PowerCapacityPerMeterUnitLocKey + ":40",
        UnitFormats.FormatPowerCapacityPerMeter(40, loc));
  }
}

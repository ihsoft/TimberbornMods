// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Localization;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>
/// Formats various units (time, distance, flow, etc.) for display in the UI, using localization for unit names and
/// formatting.
/// </summary>
public static class UnitFormats {
  public const string TickUnitLocKey = "Unit.Tick.NumberAndUnit";
  public const string HourUnitLocKey = "Unit.Hour.NumberAndUnit";
  public const string DayUnitLocKey = "Unit.Day.NumberAndUnit";
  public const string FlowUnitLocKey = "Unit.CubicMeterPerSecond.NumberAndUnit";
  public const string DistanceUnitLocKey = "Unit.Meter.NumberAndUnit";
  public const string AngleUnitLocKey = "Unit.Degree.NumberAndUnit";
  public const string PowerUnitLocKey = "Unit.HorsePower.NumberAndUnit";
  public const string PowerCapacityUnitLocKey = "Unit.HorsePowerHour.NumberAndUnit";
  public const string PowerCapacityPerMeterUnitLocKey = "Unit.HorsePowerHourPerMeter.NumberAndUnit";

  public static string FormatTicks(int value, ILoc loc) {
    return loc.T(TickUnitLocKey, value);
  }

  public static string FormatHours(int value, ILoc loc) {
    return loc.T(HourUnitLocKey, $"{value}");
  }

  public static string FormatHours(string value, ILoc loc) {
    return loc.T(HourUnitLocKey, value);
  }

  public static string FormatDays(float value, ILoc loc) {
    return loc.T(DayUnitLocKey, $"{value:F1}");
  }

  public static string FormatDays(string value, ILoc loc) {
    return loc.T(DayUnitLocKey, value);
  }

  public static string FormatFlow(float value, ILoc loc) {
    return loc.T(FlowUnitLocKey, $"{value:F2}");
  }

  public static string FormatDistance(float value, ILoc loc) {
    return loc.T(DistanceUnitLocKey, $"{value:F2}");
  }

  public static string FormatDistance(int value, ILoc loc) {
    return loc.T(DistanceUnitLocKey, $"{value}");
  }

  public static string FormatAngle(int value, ILoc loc) {
    return loc.T(AngleUnitLocKey, value);
  }

  public static string FormatPower(int value, ILoc loc) {
    return loc.T(PowerUnitLocKey, value);
  }

  public static string FormatPowerCapacity(int value, ILoc loc) {
    return loc.T(PowerCapacityUnitLocKey, value);
  }

  public static string FormatPowerCapacityPerMeter(int value, ILoc loc) {
    return loc.T(PowerCapacityPerMeterUnitLocKey, value);
  }
}

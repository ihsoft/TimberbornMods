﻿// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.Localization;
using UnityEngine;

namespace IgorZ.TimberCommons.Common {

/// <summary>Helper class that formats the duration value in the most human form.</summary>
public static class HoursShortFormatter {
  const string DaysLocKey = "Time.DaysShort";
  const string HoursLocKey = "Time.HoursShort";

  /// <summary>Gives a VERY short form of the "hours amount".</summary>
  public static string Format(ILoc loc, float value) {
    var days = Mathf.FloorToInt(value / 24f);
    var hours = Mathf.RoundToInt(value % 24f);
    if (days == 0) {
      return loc.T(HoursLocKey, hours.ToString());
    }
    if (hours == 0) {
      return loc.T(DaysLocKey, days.ToString());
    }
    var daysStr = loc.T(DaysLocKey, days.ToString());
    var hoursStr = loc.T(HoursLocKey, hours.ToString());
    return daysStr + " " + hoursStr;
  }
}

}
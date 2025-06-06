﻿// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Timberborn.Localization;
using UnityEngine;

// ReSharper disable UnusedMember.Local
// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.UI;

/// <summary>Utility class to format strings for UI.</summary>
public static class CommonFormats {
  const string DaysLocKey = "Time.DaysShort";
  const string HoursLocKey = "Time.HoursShort";
  const string SupplyRemainingLocKey = "GoodConsuming.SupplyRemaining";
  static string _localizedSupplyRemainingTmpl;

  static readonly string RedHighlight = ColorUtility.ToHtmlStringRGB(new Color(1f, 0.3f, 0.3f));
  static readonly string GreenHighlight = ColorUtility.ToHtmlStringRGB(new Color(0.35f, 1f, 0.38f));
  static readonly string YellowHighlight = ColorUtility.ToHtmlStringRGB(new Color(1f, 1f, 0.1f));

  /// <summary>Reset static caches of localized strings.</summary>
  /// <remarks>
  /// Call it from the configurator to pick up the current game language. If not called, then the cached strings won't
  /// change until the next game restart.
  /// </remarks>
  public static void ResetCachedLocStrings() {
    _localizedSupplyRemainingTmpl = null;
  }

  /// <summary>Formats a string for the "supply last" case.</summary>
  /// <remarks>
  /// It makes "supply lasts" localized string from the stock message. The cached value won;t update until game restart
  /// or <see cref="ResetCachedLocStrings"/> is called.
  /// </remarks>
  /// <seealso cref="ResetCachedLocStrings"/>
  public static string FormatSupplyLeft(ILoc loc, float hours) {
    if (_localizedSupplyRemainingTmpl == null) {
      var original = loc.T(SupplyRemainingLocKey, "###");
      _localizedSupplyRemainingTmpl = original.Substring(0, original.IndexOf("###", StringComparison.Ordinal)) + "{0}";
    }
    return string.Format(_localizedSupplyRemainingTmpl, DaysHoursFormat(loc, hours));
  }

  /// <summary>Gives a VERY short form of the "hour amount".</summary>
  public static string DaysHoursFormat(ILoc loc, float hoursAmount) {
    switch (hoursAmount) {
      case <= 0.01f:
        return loc.T(HoursLocKey, "0");
      case < 1f:
        return loc.T(HoursLocKey, hoursAmount.ToString("0.##"));
      case < 10f:
        return loc.T(HoursLocKey, hoursAmount.ToString("0.#"));
    }
    var days = Mathf.FloorToInt(hoursAmount / 24f);
    var hours = Mathf.RoundToInt(hoursAmount % 24f);
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

  /// <summary>Formats a value to keep it compact, but still representing small values.</summary>
  /// <param name="value">The value to format.</param>
  public static string FormatSmallValue(float value) {
    return value switch {
        >= 10f => value.ToString("F0"),
        >= 1f => value.ToString("0.#"),
        >= 0.1f => value.ToString("0.0#"),
        _ => value.ToString("0.00#")
    };
  }

  /// <summary>Highlights the text in red color.</summary>
  public static string HighlightRed(string text) => $"<color=#{RedHighlight}>{text}</color>";

  /// <summary>Highlights the text in green color.</summary>
  public static string HighlightGreen(string text) => $"<color=#{GreenHighlight}>{text}</color>";

  /// <summary>Highlights the text in yellow color.</summary>
  public static string HighlightYellow(string text) => $"<color=#{YellowHighlight}>{text}</color>";

  /// <summary>Highlights the text in the specified color.</summary>
  public static string Highlight(string text, Color color) {
    return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
  }

  /// <summary>Adds a strikethrough effect to the text.</summary>
  public static string Strikethrough(string text) {
    return $"<s>{text}</s>";
  }
}

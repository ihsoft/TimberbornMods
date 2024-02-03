// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.Utils {

/// <summary>Utility class to format float values.</summary>
public static class FloatValueFormatter {
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
}

}

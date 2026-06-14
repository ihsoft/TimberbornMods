using System;
using System.Globalization;

namespace UnityEngine;

public readonly struct Color {
  public readonly float r;
  public readonly float g;
  public readonly float b;

  public Color(float r, float g, float b) {
    this.r = r;
    this.g = g;
    this.b = b;
  }
}

public static class ColorUtility {
  public static string ToHtmlStringRGB(Color color) {
    return ToByte(color.r).ToString("X2", CultureInfo.InvariantCulture)
        + ToByte(color.g).ToString("X2", CultureInfo.InvariantCulture)
        + ToByte(color.b).ToString("X2", CultureInfo.InvariantCulture);
  }

  static int ToByte(float value) {
    return Math.Clamp((int)Math.Round(value * 255f), 0, 255);
  }
}

public static class Mathf {
  public static int FloorToInt(float value) {
    return (int)Math.Floor(value);
  }

  public static int RoundToInt(float value) {
    return (int)Math.Round(value);
  }
}

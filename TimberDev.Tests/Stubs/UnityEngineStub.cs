using System;
using System.Globalization;

namespace UnityEngine;

public static class Time {
  public static float unscaledTime;
}

public static class Resources {
  public static T Load<T>(string path) where T : new() {
    return new T();
  }
}

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

public sealed class Sprite {
}

public struct Vector2 {
  public float x { get; set; }
  public float y { get; set; }

  public Vector2(float x, float y) {
    this.x = x;
    this.y = y;
  }
}

public readonly struct Vector3 {
  public float x { get; }
  public float y { get; }
  public float z { get; }

  public Vector3(float x, float y, float z) {
    this.x = x;
    this.y = y;
    this.z = z;
  }
}

public readonly struct Vector2Int {
  public readonly int x;
  public readonly int y;

  public Vector2Int(int x, int y) {
    this.x = x;
    this.y = y;
  }
}

public readonly struct Vector3Int {
  public readonly int x;
  public readonly int y;
  public readonly int z;

  public Vector3Int(int x, int y, int z) {
    this.x = x;
    this.y = y;
    this.z = z;
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
  public static float Round(float value) {
    return (float)Math.Round(value);
  }

  public static int FloorToInt(float value) {
    return (int)Math.Floor(value);
  }

  public static int RoundToInt(float value) {
    return (int)Math.Round(value);
  }
}

using System;

namespace UnityEngine;

public readonly record struct Vector3Int(int x, int y, int z);

public readonly record struct Vector2Int(int x, int y);

public readonly record struct Vector2(float x, float y) {
  public float magnitude => (float)Math.Sqrt(x * x + y * y);
}

public static class Mathf {
  public const float Epsilon = 1.17549435E-38f;

  public static float Abs(float value) {
    return Math.Abs(value);
  }

  public static int RoundToInt(float value) {
    return (int)Math.Round(value);
  }

  public static bool Approximately(float a, float b) {
    return Math.Abs(a - b) < 0.00001f;
  }
}

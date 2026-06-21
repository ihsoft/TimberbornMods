using System;

namespace UnityEngine;

public readonly record struct Vector3Int(int x, int y, int z);

public static class Mathf {
  public const float Epsilon = 1.17549435E-38f;

  public static float Abs(float value) {
    return Math.Abs(value);
  }
}

using System;

namespace UnityEngine;

public static class Mathf {
  public static int CeilToInt(float value) {
    return (int)Math.Ceiling(value);
  }
}

public static class Time {
  public static float timeScale = 1f;
}

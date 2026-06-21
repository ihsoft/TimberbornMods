using System;

namespace TimberCommons.Tests;

static class Assert {
  public static void Equal<T>(T expected, T actual) {
    if (!Equals(expected, actual)) {
      throw new Exception($"Expected: {expected}. Actual: {actual}.");
    }
  }

  public static void Equal(float expected, float actual, float tolerance = 0.0001f) {
    if (Math.Abs(expected - actual) > tolerance) {
      throw new Exception($"Expected: {expected}. Actual: {actual}.");
    }
  }

  public static void True(bool actual) {
    if (!actual) {
      throw new Exception("Expected true.");
    }
  }

  public static void False(bool actual) {
    if (actual) {
      throw new Exception("Expected false.");
    }
  }
}

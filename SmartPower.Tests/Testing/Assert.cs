using System;

namespace SmartPower.Tests;

static class Assert {
  public static void Equal<T>(T expected, T actual) {
    if (!Equals(expected, actual)) {
      throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
    }
  }

  public static void True(bool condition, string message = null) {
    if (!condition) {
      throw new InvalidOperationException(message ?? "Expected condition to be true.");
    }
  }

  public static void False(bool condition, string message = null) {
    if (condition) {
      throw new InvalidOperationException(message ?? "Expected condition to be false.");
    }
  }
}

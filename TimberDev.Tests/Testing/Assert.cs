using System;

namespace TimberDev.Tests;

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

  public static T Throws<T>(Action action) where T : Exception {
    try {
      action();
    } catch (T e) {
      return e;
    } catch (Exception e) {
      throw new InvalidOperationException($"Expected {typeof(T).Name}, got {e.GetType().Name}.", e);
    }

    throw new InvalidOperationException($"Expected {typeof(T).Name}, but no exception was thrown.");
  }
}

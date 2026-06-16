using System;
using System.Collections.Generic;

namespace Automation.Tests;

static class Assert {
  public static void Equal<T>(T expected, T actual) {
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
      throw new InvalidOperationException($"Expected <{expected}>, actual <{actual}>.");
    }
  }

  public static void Same(object expected, object actual) {
    if (!ReferenceEquals(expected, actual)) {
      throw new InvalidOperationException($"Expected same reference, got <{expected}> and <{actual}>.");
    }
  }

  public static void True(bool condition, string message = null) {
    if (!condition) {
      throw new InvalidOperationException(message ?? "Expected true.");
    }
  }

  public static void False(bool condition, string message = null) {
    if (condition) {
      throw new InvalidOperationException(message ?? "Expected false.");
    }
  }

  public static T Throws<T>(Action action) where T : Exception {
    try {
      action();
    } catch (T e) {
      return e;
    }
    throw new InvalidOperationException($"Expected exception of type {typeof(T).Name}.");
  }
}

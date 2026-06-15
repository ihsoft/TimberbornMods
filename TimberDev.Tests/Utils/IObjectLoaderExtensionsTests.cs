using IgorZ.TimberDev.Utils;
using Timberborn.Persistence;

namespace TimberDev.Tests;

static class IObjectLoaderExtensionsTests {
  public static void ReturnsDefaultsForMissingValues() {
    var loader = new TestObjectLoader();

    Assert.Equal(false, loader.GetValueOrDefault(new PropertyKey<bool>("bool")));
    Assert.Equal(true, loader.GetValueOrDefault(new PropertyKey<bool>("bool"), true));
    Assert.Equal(7, loader.GetValueOrDefault(new PropertyKey<int>("int"), 7));
    Assert.Equal(1.5f, loader.GetValueOrDefault(new PropertyKey<float>("float"), 1.5f));
    Assert.Equal("fallback", loader.GetValueOrDefault(new PropertyKey<string>("string"), "fallback"));
    Assert.Equal(null, loader.GetValueOrNull(new PropertyKey<string>("nullable"), new TestStringSerializer()));
  }

  public static void ReturnsStoredValues() {
    var loader = new TestObjectLoader();
    loader.Set(new PropertyKey<bool>("bool"), true);
    loader.Set(new PropertyKey<int>("int"), 42);
    loader.Set(new PropertyKey<float>("float"), 2.5f);
    loader.Set(new PropertyKey<string>("string"), "value");

    Assert.Equal(true, loader.GetValueOrDefault(new PropertyKey<bool>("bool")));
    Assert.Equal(42, loader.GetValueOrDefault(new PropertyKey<int>("int")));
    Assert.Equal(2.5f, loader.GetValueOrDefault(new PropertyKey<float>("float")));
    Assert.Equal("value", loader.GetValueOrDefault(new PropertyKey<string>("string")));
    Assert.Equal("value", loader.GetValueOrNull(new PropertyKey<string>("string"), new TestStringSerializer()));
  }

  sealed class TestStringSerializer : IValueSerializer<string> {
  }
}

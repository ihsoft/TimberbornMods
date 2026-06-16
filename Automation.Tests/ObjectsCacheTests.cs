using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

namespace Automation.Tests;

static class ObjectsCacheTests {
  public static void CachesObjectsBySingleKey() {
    var cache = new ObjectsCache<object>();
    var created = 0;

    var first = cache.GetOrAdd("key", _ => {
      created++;
      return new object();
    });
    var second = cache.GetOrAdd("key", _ => {
      created++;
      return new object();
    });

    Assert.Same(first, second);
    Assert.Equal(1, created);
  }

  public static void CachesObjectsByCompositeKeys() {
    var cache = new ObjectsCache<string>();
    var created = 0;

    var first = cache.GetOrAdd("key", 1, true, 2.0f, (key, number, flag, value) => {
      created++;
      return $"{key}:{number}:{flag}:{value}";
    });
    var second = cache.GetOrAdd("key", 1, true, 2.0f, (key, number, flag, value) => {
      created++;
      return $"{key}:{number}:{flag}:{value}:again";
    });
    var third = cache.GetOrAdd("key", 2, true, 2.0f, (key, number, flag, value) => {
      created++;
      return $"{key}:{number}:{flag}:{value}";
    });

    Assert.Same(first, second);
    Assert.Equal("key:2:True:2", third);
    Assert.Equal(2, created);
  }
}

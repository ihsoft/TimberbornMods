using System.Collections.Generic;

namespace Timberborn.AssetSystem;

public interface IAssetLoader {
  T Load<T>(string path) where T : new();
}

public sealed class TestAssetLoader : IAssetLoader {
  public readonly List<string> Paths = new();

  public T Load<T>(string path) where T : new() {
    Paths.Add(path);
    return new T();
  }
}

// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json;

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

/// <summary>Exact map facts extracted from one downloaded Workshop archive.</summary>
sealed record MapArchiveAnalysis {
  public MapArchiveAnalysis(
      int width, int height, IReadOnlyDictionary<string, JsonElement> classifications) {
    Width = width;
    Height = height;
    Classifications = classifications;
  }

  /// <summary>Playable map width in tiles.</summary>
  public int Width { get; }

  /// <summary>Playable map height in tiles.</summary>
  public int Height { get; }

  /// <summary>Classifier results keyed by their stable public index feature names.</summary>
  public IReadOnlyDictionary<string, JsonElement> Classifications { get; }
}

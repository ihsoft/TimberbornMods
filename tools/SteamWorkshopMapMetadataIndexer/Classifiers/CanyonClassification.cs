// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text.Json.Serialization;

namespace IgorZ.MapBrowser.WorkshopMapIndexing.Classifiers;

/// <summary>Projected measurements of one connected canyon system.</summary>
sealed record CanyonClassification {
  public CanyonClassification(double length, double averageWidth, double medianBankHeight) {
    Length = length;
    AverageWidth = averageWidth;
    MedianBankHeight = medianBankHeight;
  }

  /// <summary>Length of the longest connected route through the canyon floor, in tiles.</summary>
  [JsonPropertyName("length")]
  public double Length { get; }

  /// <summary>Average projected width of the canyon floor, in tiles.</summary>
  [JsonPropertyName("average_width")]
  public double AverageWidth { get; }

  /// <summary>Median height of the lower opposing bank, in terrain levels.</summary>
  [JsonPropertyName("median_bank_height")]
  public double MedianBankHeight { get; }
}

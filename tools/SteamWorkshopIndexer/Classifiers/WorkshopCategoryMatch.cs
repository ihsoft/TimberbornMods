// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopIndexing.Classifiers;

/// <summary>A category candidate with its accumulated evidence score and auditable evidence labels.</summary>
sealed class WorkshopCategoryMatch {
  /// <summary>Creates an auditable match from the category label, accumulated score, and supporting evidence.</summary>
  public WorkshopCategoryMatch(string category, int score, List<string> evidence) {
    Category = category;
    Score = score;
    Evidence = evidence;
  }

  /// <summary>The stable category label published in the Workshop snapshot.</summary>
  public string Category { get; }

  /// <summary>The sum of tag and term evidence weights for this category.</summary>
  public int Score { get; }

  /// <summary>The matched tags and terms that explain the score.</summary>
  public List<string> Evidence { get; }
}

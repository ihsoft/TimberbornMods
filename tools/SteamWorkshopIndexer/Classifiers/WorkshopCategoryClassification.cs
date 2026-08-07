// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopIndexing.Classifiers;

/// <summary>Coarse searchable category evidence derived from public Workshop metadata.</summary>
sealed class WorkshopCategoryClassification {
  /// <summary>Creates the published primary category and its supporting candidate matches.</summary>
  public WorkshopCategoryClassification(string primaryCategory, List<WorkshopCategoryMatch> matches) {
    PrimaryCategory = primaryCategory;
    Matches = matches;
  }

  /// <summary>
  /// The strongest category label, with the exact Steam Map tag taking precedence over text evidence.
  /// </summary>
  public string PrimaryCategory { get; }

  /// <summary>All category matches and the metadata evidence contributing to their scores.</summary>
  public List<WorkshopCategoryMatch> Matches { get; }
}

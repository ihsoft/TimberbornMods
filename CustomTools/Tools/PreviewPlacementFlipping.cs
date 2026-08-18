// Timberborn Custom Tools
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;

namespace IgorZ.CustomTools.Tools;

static class PreviewPlacementFlipping {
  /// <summary>Synchronizes the shared preview flipping state with the selected block object template.</summary>
  public static void Configure(PreviewPlacement previewPlacement, BlockObjectSpec blockObjectSpec) {
    if (blockObjectSpec.Flippable) {
      previewPlacement.EnableFlipping();
    } else {
      previewPlacement.DisableFlipping();
    }
  }
}

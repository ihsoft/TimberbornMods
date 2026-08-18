using IgorZ.CustomTools.Tools;
using Timberborn.BlockObjectTools;
using Timberborn.BlockSystem;

namespace CustomTools.Tests;

static class PreviewPlacementFlippingTests {
  public static void DisablesFlippingForNonFlippableTemplate() {
    var previewPlacement = new PreviewPlacement();

    PreviewPlacementFlipping.Configure(previewPlacement, new BlockObjectSpec { Flippable = false });

    Assert.False(previewPlacement.FlippingEnabled);
  }

  public static void EnablesFlippingForFlippableTemplate() {
    var previewPlacement = new PreviewPlacement();

    PreviewPlacementFlipping.Configure(previewPlacement, new BlockObjectSpec { Flippable = true });

    Assert.True(previewPlacement.FlippingEnabled);
  }
}

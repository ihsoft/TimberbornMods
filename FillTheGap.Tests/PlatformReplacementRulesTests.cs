// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.FillTheGap.Core;

namespace IgorZ.FillTheGap.Tests;

static class PlatformReplacementRulesTests {
  public static void MapsFolktails() {
    AssertDefinition("Platform.Folktails", 1, null);
    AssertDefinition("DoublePlatform.Folktails", 2, "Platform.Folktails");
    AssertDefinition("TriplePlatform.Folktails", 3, "DoublePlatform.Folktails");
  }

  public static void MapsIronTeeth() {
    AssertDefinition("Platform.IronTeeth", 1, null);
    AssertDefinition("DoublePlatform.IronTeeth", 2, "Platform.IronTeeth");
    AssertDefinition("TriplePlatform.IronTeeth", 3, "DoublePlatform.IronTeeth");
  }

  public static void RejectsUnknownTemplate() {
    Assert.False(PlatformReplacementRules.TryGetDefinition("LargePlatform.Folktails", out _));
  }

  public static void ContainsOccupiedLevels() {
    const int BaseZ = 7;
    Assert.False(PlatformReplacementRules.ContainsLevel(BaseZ, 3, BaseZ - 1));
    Assert.True(PlatformReplacementRules.ContainsLevel(BaseZ, 3, BaseZ));
    Assert.True(PlatformReplacementRules.ContainsLevel(BaseZ, 3, BaseZ + 1));
    Assert.True(PlatformReplacementRules.ContainsLevel(BaseZ, 3, BaseZ + 2));
    Assert.False(PlatformReplacementRules.ContainsLevel(BaseZ, 3, BaseZ + 3));
  }

  public static void FindsTopLevel() {
    const int BaseZ = 7;
    Assert.True(PlatformReplacementRules.HasSurfaceAbove(BaseZ, 1, BaseZ));
    Assert.False(PlatformReplacementRules.HasSurfaceAbove(BaseZ, 2, BaseZ));
    Assert.True(PlatformReplacementRules.HasSurfaceAbove(BaseZ, 2, BaseZ + 1));
    Assert.False(PlatformReplacementRules.HasSurfaceAbove(BaseZ, 3, BaseZ + 1));
    Assert.True(PlatformReplacementRules.HasSurfaceAbove(BaseZ, 3, BaseZ + 2));
  }

  static void AssertDefinition(string templateName, int height, string replacementTemplateName) {
    Assert.True(PlatformReplacementRules.TryGetDefinition(templateName, out var definition));
    Assert.Equal(height, definition.Height);
    Assert.Equal(replacementTemplateName, definition.ReplacementTemplateName);
  }
}

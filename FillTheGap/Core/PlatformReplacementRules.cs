// Timberborn Mod: Fill The Gap
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;

namespace IgorZ.FillTheGap.Core;

sealed record SupportedPlatformDefinition(int Height, string ReplacementTemplateName);

static class PlatformReplacementRules {
  static readonly Dictionary<string, SupportedPlatformDefinition> PlatformDefinitions = new() {
      ["Platform.Folktails"] = new SupportedPlatformDefinition(1, null),
      ["DoublePlatform.Folktails"] = new SupportedPlatformDefinition(2, "Platform.Folktails"),
      ["TriplePlatform.Folktails"] = new SupportedPlatformDefinition(3, "DoublePlatform.Folktails"),
      ["Platform.IronTeeth"] = new SupportedPlatformDefinition(1, null),
      ["DoublePlatform.IronTeeth"] = new SupportedPlatformDefinition(2, "Platform.IronTeeth"),
      ["TriplePlatform.IronTeeth"] = new SupportedPlatformDefinition(3, "DoublePlatform.IronTeeth"),
  };

  public static bool TryGetDefinition(string templateName, out SupportedPlatformDefinition definition) {
    return PlatformDefinitions.TryGetValue(templateName, out definition);
  }

  public static bool ContainsLevel(int platformBaseZ, int platformHeight, int coordinatesZ) {
    return coordinatesZ >= platformBaseZ && coordinatesZ < platformBaseZ + platformHeight;
  }

  public static bool HasSurfaceAbove(int platformBaseZ, int platformHeight, int coordinatesZ) {
    return coordinatesZ == platformBaseZ + platformHeight - 1;
  }
}

// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;

namespace IgorZ.TimberCommons.CommonUIPatches;

static class GameSaveVersionFormatter {
  public static string Format(string numericVersion) {
    var versionParts = numericVersion.Split('.');
    var visibleParts = Math.Min(versionParts.Length, 4);
    while (visibleParts > 2 && versionParts[visibleParts - 1] == "0") {
      visibleParts--;
    }
    return "v" + string.Join(".", versionParts, 0, visibleParts);
  }
}

// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Utils;
using UnityDev.Utils.LogUtilsLite;

// ReSharper disable once CheckNamespace
namespace IgorZ.SmartPower {

static class Features {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  static Features() {
    FeatureController.ReadFeatures(Consume);
  }

  static bool Consume(string featureName, bool isEnabled) {
    switch (featureName) {
      case "DebugEx.VerboseLogging":
        DebugExVerboseLogging = isEnabled;
        return true;
      default:
        return false;
    }
  }
}

}

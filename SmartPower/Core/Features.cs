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

  /// <summary>
  /// Indicates that the power network fragment should show the battery vitals (capacity/charge and flow).
  /// </summary>
  public static bool NetworkShowBatteryStats;

  static Features() {
    FeatureController.ReadFeatures(Consume);
  }

  static bool Consume(string featureName, bool isEnabled) {
    switch (featureName) {
      case "DebugEx.VerboseLogging":
        DebugExVerboseLogging = isEnabled;
        return true;
      case "Network.ShowBatteryVitals":
        NetworkShowBatteryStats = isEnabled;
        return true;
      default:
        return false;
    }
  }
}

}

// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberDev.Utils;
using Timberborn.GoodConsumingBuildingSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.Common {

static class Features {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  /// <summary>
  /// Indicates that duration in the "supply lasts for" message on <see cref="GoodConsumingBuilding"/> UI should be
  /// formatted as "Xd Yh" instead of "XX hours".
  /// </summary>
  /// <seealso cref="HoursShortFormatter"/>
  public static bool GoodConsumingBuildingUIDaysHoursForAll;

  static Features() {
    FeatureController.ReadFeatures(Consume);
  }

  static bool Consume(string featureName, bool isEnabled) {
    switch (featureName) {
      case "DebugEx.VerboseLogging":
        DebugExVerboseLogging = isEnabled;
        return true;
      case "GoodConsumingBuildingUI.DaysHoursViewForAllBuildings":
        GoodConsumingBuildingUIDaysHoursForAll = isEnabled;
        return true;
      default:
        return false;
    }
  }
}

}

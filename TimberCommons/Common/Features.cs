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

  /// <summary>
  /// Indicates that durations for the growth time for all growables should be formatted as "Xd Yh" instead of rounding
  /// to days.
  /// </summary>
  /// <seealso cref="HoursShortFormatter"/>
  public static bool GrowableGrowthTimeUIDaysHoursViewForAll;

  static Features() {
    FeatureController.ReadFeatures(Consume);
  }

  static bool Consume(string name, bool enabled, string value) {
    return name switch {
        "DebugEx.VerboseLogging" =>
            FeatureController.SetFlag(ref DebugExVerboseLogging, name, enabled, value),
        "GoodConsumingBuildingUI.DaysHoursViewForAllBuildings" =>
            FeatureController.SetFlag(ref GoodConsumingBuildingUIDaysHoursForAll, name, enabled, value),
        "GrowableGrowthTimeUI.DaysHoursViewForAllGrowables" =>
            FeatureController.SetFlag(ref GrowableGrowthTimeUIDaysHoursViewForAll, name, enabled, value),
        _ => false
    };
  }
}

}

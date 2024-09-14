// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.GoodConsumingBuildingSystem;
using Timberborn.ModManagerScene;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.Common {

sealed class Features : IModStarter {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  /// <summary>
  /// Indicates that duration in the "supply lasts for" message on <see cref="GoodConsumingBuilding"/> UI should be
  /// formatted as "Xd Yh" instead of "XX hours".
  /// </summary>
  /// <seealso cref="CommonFormats.DaysHoursFormat"/>
  public static bool GoodConsumingBuildingUIDaysHoursForAll;

  /// <summary>
  /// Indicates that durations for the growth time for all growables should be formatted as "Xd Yh" instead of rounding
  /// to days.
  /// </summary>
  /// <seealso cref="CommonFormats.DaysHoursFormat"/>
  public static bool GrowableGrowthTimeUIDaysHoursViewForAll;

  /// <summary>Indicates whether recipe durations exceeding 24 hours should be displayed in days/hours format.</summary>
  /// <seealso cref="CommonFormats.DaysHoursFormat"/>
  public static bool ShowDaysHoursForSlowRecipes;

  /// <summary>
  /// Specifies whether fuel rates below 0.1 should be displayed with increased precision in the recipe UI.
  /// </summary>
  /// <seealso cref="CommonFormats.FormatSmallValue"/>
  public static bool ShowLongValueForLowFuelConsumptionRecipes;

  /// <summary>Overrides the maximum registry size for PrefabOptimizer to suppress log complaints.</summary>
  public static int PrefabOptimizerMaxExpectedRegistrySize = -1;

  /// <summary>
  /// Specifies whether the terrain view should be adjusted to present irrigated tiles as "well moisturized". Otherwise,
  /// the stock logic will decide based on the moisture level.
  /// </summary>
  public static bool OverrideDesertLevelsForWaterTowers;

  /// <summary>Specifies whether the water output depth at the spillway should be adjustable.</summary>
  public static bool AdjustWaterOutputWaterDepthAtSpillway;

  /// <summary>Specifies whether no UI changes to the stock logic must be made by the mod.</summary>
  /// <remarks>
  /// It's a super setting to any UI affecting Harmony patches. If the game doesn't work well or the mod doesn't load,
  /// enable this feature and only the stock UI will be in action.
  /// </remarks>
  public static bool DisableAllUiPatches;

  /// <summary>Specifies whether beavers that move underground should be checked for badwater exposure.</summary>
  /// <remarks>
  /// The stock game doesn't allow beavers to be underground, but there are mods that provide underground buildings like
  /// tunnels (e.g. "Path Extention" mod).
  /// </remarks>
  public static bool NoContaminationUnderground;

  /// <inheritdoc/>
  public void StartMod() { 
    throw new Exception("We're not supposed to be here!");
  }

  /// <inheritdoc/>
  public void StartMod(IModEnvironment modEnvironment) {
    FeatureController.ReadFeatures(modEnvironment.ModPath, Consume);
  }

  static bool Consume(string name, bool enabled, string value) {
    return name switch {
        "DebugEx.VerboseLogging" =>
            FeatureController.SetFlag(ref DebugExVerboseLogging, name, enabled, value),
        "CommonUI.DisableAllPatches" =>
            FeatureController.SetFlag(ref DisableAllUiPatches, name, enabled, value),
        "GoodConsumingBuildingUI.DaysHoursViewForAllBuildings" =>
            FeatureController.SetFlag(ref GoodConsumingBuildingUIDaysHoursForAll, name, enabled, value),
        "GrowableGrowthTimeUI.DaysHoursViewForAllGrowables" =>
            FeatureController.SetFlag(ref GrowableGrowthTimeUIDaysHoursViewForAll, name, enabled, value),
        "RecipesUI.ShowDaysHoursForSlowRecipes" =>
            FeatureController.SetFlag(ref ShowDaysHoursForSlowRecipes, name, enabled, value),
        "RecipesUI.ShowLongValueForLowFuelConsumptionRecipes" =>
            FeatureController.SetFlag(ref ShowLongValueForLowFuelConsumptionRecipes, name, enabled, value),
        "PrefabOptimizer.MaxExpectedRegistrySize" =>
            FeatureController.SetValue(ref PrefabOptimizerMaxExpectedRegistrySize, name, enabled, value),
        "WaterBuildings.AdjustableWaterOutput" =>
                FeatureController.SetFlag(ref AdjustWaterOutputWaterDepthAtSpillway, name, enabled, value),
        "WaterTowers.OverrideDesertLevels" =>
            FeatureController.SetFlag(ref OverrideDesertLevelsForWaterTowers, name, enabled, value),
        "CommonQoL.NoContaminationUnderground" =>
            FeatureController.SetFlag(ref NoContaminationUnderground, name, enabled, value),
        _ => false
    };
  }
}

}

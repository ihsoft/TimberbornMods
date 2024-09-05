// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using IgorZ.TimberDev.Utils;
using Timberborn.ModManagerScene;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.SmartPower.Core {

// ReSharper disable once ClassNeverInstantiated.Global
sealed class Features : IModStarter {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  /// <summary>
  /// Indicates that the power network fragment should show the battery vitals (capacity/charge and flow).
  /// </summary>
  public static bool NetworkShowBatteryStats;

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
        "DebugEx.VerboseLogging" => FeatureController.SetFlag(ref DebugExVerboseLogging, name, enabled, value),
        "Network.ShowBatteryVitals" => FeatureController.SetFlag(ref NetworkShowBatteryStats, name, enabled, value),
        _ => false
    };
  }
}

}

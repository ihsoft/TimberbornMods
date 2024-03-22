// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.Core {

[Configurator(SceneEntrypoint.All)]
sealed class Features : IConfigurator {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  /// <summary>Specifies whether the path controller should periodically dump performance statistics.</summary>
  public static bool PathCheckingControllerProfiling;

  public void Configure(IContainerDefinition containerDefinition) {
    if (DebugExVerboseLogging && DebugEx.LoggingSettings.VerbosityLevel < 5) {
      DebugEx.LoggingSettings.VerbosityLevel = 5;
    }
  }

  static Features() {
    FeatureController.ReadFeatures(Consume);
  }

  static bool Consume(string name, bool enabled, string value) {
    return name switch {
        "DebugEx.VerboseLogging" => FeatureController.SetFlag(ref DebugExVerboseLogging, name, enabled, value),
        "PathCheckingController.Profiling" => FeatureController.SetFlag(ref PathCheckingControllerProfiling, name, enabled, value),
        _ => false
    };
  }
}

}

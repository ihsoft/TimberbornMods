// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Utils;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using UnityDev.Utils.LogUtilsLite;

namespace Automation.AutomationSystem {

[Configurator(SceneEntrypoint.All)]
sealed class Features : IConfigurator {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  /// <summary>Specifies whether path checking system should periodically dump performance statistics.</summary>
  public static bool PathCheckingSystemProfiling;

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
        "DebugEx.VerboseLogging" =>
            FeatureController.SetFlag(ref DebugExVerboseLogging, name, enabled, value),
        "PathCheckingSystem.Profiling" =>
            FeatureController.SetFlag(ref PathCheckingSystemProfiling, name, enabled, value),
        _ => false
    };
  }
}

}

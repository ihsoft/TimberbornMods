// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.Common {

[Configurator(SceneEntrypoint.All)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    if (Features.DebugExVerboseLogging && DebugEx.LoggingSettings.VerbosityLevel < 5) {
      DebugEx.LoggingSettings.VerbosityLevel = 5;
    }
  }
}

}

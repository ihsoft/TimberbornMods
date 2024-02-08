// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Reflection;
using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.PrefabOptimization;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.TimberCommons.Common {

[Configurator(SceneEntrypoint.All)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    if (Features.DebugExVerboseLogging && DebugEx.LoggingSettings.VerbosityLevel < 5) {
      DebugEx.LoggingSettings.VerbosityLevel = 5;
    }
    if (Features.PrefabOptimizerMaxExpectedRegistrySize != -1) {
      var fieldInfo = typeof(EnvironmentVertexColorPrefabOptimizer).GetField(
          "MaxExpectedRegistrySize", BindingFlags.Static | BindingFlags.NonPublic);
      if (fieldInfo != null) {
        fieldInfo.SetValue(null, Features.PrefabOptimizerMaxExpectedRegistrySize);
      } else {
        DebugEx.Warning("Cannot override EnvironmentVertexColorPrefabOptimizer.MaxExpectedRegistrySize");
      }
    }
  }
}

}

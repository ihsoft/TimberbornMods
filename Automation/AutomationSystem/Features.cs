// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using Bindito.Core;
using IgorZ.TimberDev.Utils;
using Timberborn.ModManagerScene;
using UnityDev.Utils.LogUtilsLite;

namespace IgorZ.Automation.AutomationSystem;

sealed class Features : IModStarter {
  /// <summary>Indicates that <see cref="DebugEx.Fine"/> methods should emit record to the log.</summary>
  public static bool DebugExVerboseLogging;

  /// <summary>Specifies whether a path checking system should periodically dump performance statistics.</summary>
  public static bool PathCheckingSystemProfiling;

  public void Configure(IContainerDefinition containerDefinition) {
    if (DebugExVerboseLogging && DebugEx.VerbosityLevel < DebugEx.LogLevel.Finer) {
      DebugEx.VerbosityLevel = DebugEx.LogLevel.Finer;
    }
  }

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
        "PathCheckingSystem.Profiling" =>
            FeatureController.SetFlag(ref PathCheckingSystemProfiling, name, enabled, value),
        _ => false
    };
  }
}
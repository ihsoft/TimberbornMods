// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.DebuggingUI;
using Timberborn.SingletonSystem;

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>Debug panel for the GPU simulation related stuff.</summary>
public class GpuSimulatorsDebuggingPanel : ILoadableSingleton, IDebuggingPanel {
  readonly DebuggingPanel _debuggingPanel;

  GpuSimulatorsDebuggingPanel(DebuggingPanel debuggingPanel) {
    _debuggingPanel = debuggingPanel;
  }

  /// <inheritdoc/>
  public void Load() {
    _debuggingPanel.AddDebuggingPanel(this, "GPU simulations");
  }

  /// <inheritdoc/>
  public string GetText() {
    return GpuSimulatorsController.Self.GetStatsText();
  }
}

}

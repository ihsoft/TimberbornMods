// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Text;
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
    var text = new StringBuilder();
    var soilContaminationTotal = GpuSoilContaminationSimulator._lastSimulationDurationTotal * 1000;
    text.AppendLine($"Soil contamination total: {soilContaminationTotal:0.##} ms");
    var soilContaminationShader = GpuSoilContaminationSimulator._lastSimulationShaderCost * 1000;
    text.Append($"Soil contamination shader: {soilContaminationShader:0.##} ms");
    return text.ToString();
  }
}

}

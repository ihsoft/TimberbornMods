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
    IGpuSimulatorStats stats = null;
    if (GpuSoilContaminationSimulator.Self.IsEnabled) {
      stats = GpuSoilContaminationSimulator.Self;
    } else if (GpuSoilContaminationSimulator2.Self.IsEnabled) {
      stats = GpuSoilContaminationSimulator2.Self;
    }
    if (stats != null) {
      var (_, _, _, soilTotal) = stats.GetTotalStats();
      text.AppendLine($"Soil contamination total: {soilTotal * 1000:0.##} ms");
      var (_, _, _, soilShader) = stats.GetShaderStats();
      text.Append($"Soil contamination shader: {soilShader * 1000:0.##} ms");
    } else {
      text.Append("GPU simulation is disabled");
    }
    
    return text.ToString();
  }
}

}

// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.TimberCommons.GpuSimulators {

/// <summary>Common interface for the GPU simulators performance counters.</summary>
public interface IGpuSimulatorStats {
  /// <summary>
  /// Returns performance counters for the shader stage. That is, the time spent in providing data to shader,
  /// dispatching its kernels and waiting for the result.
  /// </summary>
  public (double min, double max, double avg, double mean) GetShaderStats();

  
  /// <summary>
  /// Returns performance counters for the whole update tick that involves data preparation, running the shaders, and
  /// handling the result data.
  /// </summary>
  public (double min, double max, double avg, double mean) GetTotalStats();
}

}

// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using IgorZ.Automation.PathCheckingSystem;
using Timberborn.DebuggingUI;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;

namespace IgorZ.Automation.PathCheckingSystemUI;

/// <summary>Shows various stat and debug information for the path checking system.</summary>
sealed class PathCheckingSystemDebugPanel : ILoadableSingleton, IDebuggingPanel, ITickableSingleton {

  const int TicksHistoryWindow = 10;
  readonly DebuggingPanel _debuggingPanel;
  readonly PathCheckingService _pathCheckingService;

  readonly List<float> _stateCheckUpdateTicks = new(TicksHistoryWindow);
  readonly List<float> _navMeshUpdateTicks = new(TicksHistoryWindow);
  float _navMeshUpdateTickCostMs;
  float _navMeshUpdateMaxCostMs;
  float _stateCheckTickCostMs;
  float _stateCheckMaxCostMs;

  PathCheckingSystemDebugPanel(DebuggingPanel debuggingPanel, PathCheckingService pathCheckingService) {
    _debuggingPanel = debuggingPanel;
    _pathCheckingService = pathCheckingService;
  }

  public void Load() {
    _debuggingPanel.AddDebuggingPanel(this, "Automation: path checking");
  }

  public string GetText() {
    var res = new StringBuilder();
    res.AppendFormat("Sites under control: {0}\n", _pathCheckingService.NumberOfSites);
    res.AppendFormat("NavMesh: tick={0:0.##}ms, max={1:0.##}ms\n", _navMeshUpdateTickCostMs, _navMeshUpdateMaxCostMs);
    res.AppendFormat("Check: tick={0:0.##}ms, max={1:0.##}ms", _stateCheckTickCostMs, _stateCheckMaxCostMs);
    return res.ToString();
  }

  public void Tick() {
    _stateCheckTickCostMs = PathCheckingService.PatchCheckingTimer.ElapsedTicks * 1000f / Stopwatch.Frequency;
    _stateCheckUpdateTicks.Add(_stateCheckTickCostMs);
    if (_stateCheckUpdateTicks.Count > TicksHistoryWindow) {
      _stateCheckUpdateTicks.RemoveAt(0);
    }
    _stateCheckMaxCostMs = _stateCheckUpdateTicks.Max();
    PathCheckingService.PatchCheckingTimer.Reset();
    _navMeshUpdateTickCostMs = PathCheckingSite.NavMeshUpdateTimer.ElapsedTicks * 1000f / Stopwatch.Frequency;
    _navMeshUpdateTicks.Add(_navMeshUpdateTickCostMs);
    if (_navMeshUpdateTicks.Count > TicksHistoryWindow) {
      _navMeshUpdateTicks.RemoveAt(0);
    }
    _navMeshUpdateMaxCostMs = _navMeshUpdateTicks.Max();
    PathCheckingSite.NavMeshUpdateTimer.Reset();
  }
}
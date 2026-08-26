// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.WaterSystem;
using UnityEngine;

namespace IgorZ.TimberCommons.WaterBuildingsUI;

static class ValveFlowCalculator {
  public static float GetDirectionalFlow(Vector2 flow, FlowDirection direction) {
    return direction switch {
        FlowDirection.Top => flow.y,
        FlowDirection.Right => flow.x,
        FlowDirection.Bottom => -flow.y,
        FlowDirection.Left => -flow.x,
        _ => flow.magnitude,
    };
  }
}

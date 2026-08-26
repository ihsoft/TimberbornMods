// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.TimberCommons.WaterBuildingsUI;
using Timberborn.WaterSystem;
using UnityEngine;

namespace TimberCommons.Tests;

static class ValveFlowCalculatorTests {
  public static void ProjectsFlowOntoEveryValveDirection() {
    var flow = new Vector2(1.25f, 2.5f);

    Assert.Equal(2.5f, ValveFlowCalculator.GetDirectionalFlow(flow, FlowDirection.Top));
    Assert.Equal(1.25f, ValveFlowCalculator.GetDirectionalFlow(flow, FlowDirection.Right));
    Assert.Equal(-2.5f, ValveFlowCalculator.GetDirectionalFlow(flow, FlowDirection.Bottom));
    Assert.Equal(-1.25f, ValveFlowCalculator.GetDirectionalFlow(flow, FlowDirection.Left));
  }

  public static void UsesMagnitudeWhenDirectionIsUnrestricted() {
    Assert.Equal(5f, ValveFlowCalculator.GetDirectionalFlow(new Vector2(3f, 4f), FlowDirection.Any));
  }
}

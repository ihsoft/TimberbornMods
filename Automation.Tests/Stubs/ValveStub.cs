namespace Timberborn.WaterBuildings;

public sealed class Valve : Timberborn.BaseComponentSystem.BaseComponent {
  public float MaxOutflowLimit { get; init; } = 1;
  public float OutflowLimit { get; private set; }
  public bool OutflowLimitEnabled { get; private set; }
  public int SetOutflowLimitCalls { get; private set; }
  public int SetOutflowLimitEnabledCalls { get; private set; }

  public void SetOutflowLimitEnabledAndSynchronize(bool value) {
    OutflowLimitEnabled = value;
    SetOutflowLimitEnabledCalls++;
  }

  public void SetOutflowLimitAndSynchronize(float value) {
    OutflowLimit = value;
    SetOutflowLimitCalls++;
  }
}

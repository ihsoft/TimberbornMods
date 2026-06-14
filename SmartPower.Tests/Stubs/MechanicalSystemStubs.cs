namespace Timberborn.MechanicalSystem;

using System.Collections.Generic;
using Timberborn.BaseComponentSystem;

public sealed class MechanicalGraph {
  public int BatteryCapacity { get; set; }
  public int BatteryCharge { get; set; }
  public int PowerDemand { get; set; }
  public int PowerSupply { get; set; }
  public List<MechanicalNode> Nodes { get; } = [];
}

public sealed class MechanicalNode : BaseComponent {
  public MechanicalGraph Graph { get; set; }
  public MechanicalNodeActuals Actuals { get; } = new();
  public int _nominalPowerOutput;
}

public sealed class MechanicalNodeActuals {
  public int PowerOutput { get; set; }
}

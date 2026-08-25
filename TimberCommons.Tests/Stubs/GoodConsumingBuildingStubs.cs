using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;

namespace Timberborn.GoodConsumingBuildingSystem;

public sealed class GoodConsumingBuilding : BaseComponent {
  public GoodConsumingBuildingSpec _goodConsumingBuildingSpec;
  public bool CanUse { get; set; } = true;
  public bool ConsumptionPaused { get; set; }
  public GoodConsumingToggle Toggle { get; } = new();

  public GoodConsumingToggle GetGoodConsumingToggle() {
    return Toggle;
  }
}

public sealed class GoodConsumingToggle {
  public int PauseCalls { get; private set; }
  public int ResumeCalls { get; private set; }

  public void PauseConsumption() {
    PauseCalls++;
  }

  public void ResumeConsumption() {
    ResumeCalls++;
  }
}

public sealed record GoodConsumingBuildingSpec : ComponentSpec {
  public ConsumedGoodSpec[] ConsumedGoods { get; init; } = [];
}

public sealed record ConsumedGoodSpec : ComponentSpec {
  public float GoodPerHour { get; init; }
}

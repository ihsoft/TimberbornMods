using System.Collections.Immutable;
using Timberborn.BaseComponentSystem;
using Timberborn.Goods;

namespace Timberborn.EntitySystem {
  sealed class LabeledEntitySpec {
    public string DisplayNameLocKey { get; init; }
  }
}

namespace Timberborn.TemplateSystem {
  sealed class TemplateSpec {
    public string TemplateName { get; init; }
  }
}

namespace Timberborn.Planting {
  sealed class PlantableSpec : BaseComponent {
    public string TemplateName { get; init; }

    public T GetSpec<T>() where T : class => GetComponent<T>();
  }

  sealed class PlanterBuilding : BaseComponent {
    public ImmutableArray<PlantableSpec> AllowedPlantables { get; init; } = [];
  }

  sealed class PlantablePrioritizer : BaseComponent {
    public PlantableSpec PrioritizedPlantableSpec { get; private set; }
    public int Changes { get; private set; }

    public void PrioritizePlantable(PlantableSpec plantable) {
      PrioritizedPlantableSpec = plantable;
      Changes++;
    }
  }
}

namespace Timberborn.Gathering {
  sealed class GatherableSpec : BaseComponent {
    public sealed class YieldSpec {
      public GoodAmountSpec Yield { get; init; }
    }

    public YieldSpec Yielder { get; init; }

    public T GetSpec<T>() where T : class => GetComponent<T>();
  }

  sealed class GathererFlag : BaseComponent {
    public ImmutableArray<GatherableSpec> AllowedGatherables { get; init; } = [];
  }

  sealed class GatherablePrioritizer : BaseComponent {
    public GatherableSpec PrioritizedGatherable { get; private set; }
    public int Changes { get; private set; }

    public void PrioritizeGatherable(GatherableSpec gatherable) {
      PrioritizedGatherable = gatherable;
      Changes++;
    }
  }
}

namespace Timberborn.Fields {
  sealed class FarmHouse : BaseComponent {
    public bool PlantingPrioritized { get; private set; } = true;

    public void PrioritizePlanting() {
      PlantingPrioritized = true;
    }

    public void UnprioritizePlanting() {
      PlantingPrioritized = false;
    }
  }
}

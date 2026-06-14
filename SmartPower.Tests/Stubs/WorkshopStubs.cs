using Timberborn.BaseComponentSystem;

namespace Timberborn.Workshops;

public sealed class Manufactory {
  public bool HasCurrentRecipe { get; set; }
  public bool HasAllIngredients { get; set; } = true;
  public bool HasFuel { get; set; } = true;
  public bool HasUnreservedCapacity { get; set; } = true;

  public bool HasUnreservedCapacityForCurrentProducts() {
    return HasUnreservedCapacity;
  }
}

public sealed class ProductionIncreaser : BaseComponent {
}

public sealed class Workshop : BaseComponent {
}

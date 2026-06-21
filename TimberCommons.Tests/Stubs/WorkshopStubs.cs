using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Timberborn.BaseComponentSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Goods;

namespace Timberborn.Goods {
  public readonly record struct GoodAmount(string GoodId, int Amount);

  public record GoodAmountSpec {
    public string Id { get; init; }
    public int Amount { get; init; }

    public GoodAmount ToGoodAmount() {
      return new GoodAmount(Id, Amount);
    }
  }
}

namespace Timberborn.InventorySystem {
  public sealed class Inventory : BaseComponent {
    readonly Dictionary<string, int> _amountsInStock = [];
    readonly Dictionary<string, int> _limitedAmounts = [];

    public void SetStock(string goodId, int amount, int limitedAmount) {
      _amountsInStock[goodId] = amount;
      _limitedAmounts[goodId] = limitedAmount;
    }

    public int AmountInStock(string goodId) {
      return _amountsInStock.GetValueOrDefault(goodId);
    }

    public int LimitedAmount(string goodId) {
      return _limitedAmounts.GetValueOrDefault(goodId);
    }
  }
}

namespace Timberborn.Workshops {
  public record RecipeSpec : ComponentSpec {
    public string Id { get; init; }
    public float CycleDurationInHours { get; init; }
    public ImmutableArray<GoodAmountSpec> Ingredients { get; init; } = [];
    public ImmutableArray<GoodAmountSpec> Products { get; init; } = [];
    public string Fuel { get; init; }
    public int CyclesFuelLasts { get; init; }
    public int FuelCapacity { get; init; }
    public bool ConsumesFuel => FuelCapacity > 0;
  }

  public sealed class Manufactory : BaseComponent {
    public event EventHandler RecipeChanged;
    public InventorySystem.Inventory Inventory { get; init; }
    public float FuelRemaining { get; init; }
    public float ProductionProgress { get; private set; }
    public bool IsReadyToProduce { get; set; } = true;
    public RecipeSpec CurrentRecipe { get; set; }
    public ImmutableArray<RecipeSpec> ProductionRecipes { get; init; } = [];
    public bool HasCurrentRecipe => CurrentRecipe != null;
    public float ProductionEfficiencyValue { get; set; } = 1f;

    public void IncreaseProductionProgress(float workedHours) {
      ProductionProgress += workedHours / CurrentRecipe.CycleDurationInHours;
    }

    public float ProductionEfficiency() {
      return ProductionEfficiencyValue;
    }

    public void SetRecipe(RecipeSpec recipe) {
      CurrentRecipe = recipe;
      RecipeChanged?.Invoke(this, EventArgs.Empty);
    }
  }
}

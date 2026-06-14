using IgorZ.SmartPower.PowerConsumers;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;
using Timberborn.StatusSystem;
using Timberborn.Workshops;

namespace SmartPower.Tests;

static class SmartManufactoryTests {
  public static void StandbyOnMissingIngredients() {
    var manufactory = CreateManufactory(
        nominalPower: 20,
        hasCurrentRecipe: true,
        hasAllIngredients: false,
        hasFuel: true,
        hasCapacity: true);

    Assert.Equal(2, manufactory.UpdateAndGetPowerInput());
    Assert.True(manufactory.StandbyMode);
    Assert.True(manufactory.MissingIngredients);
  }

  public static void NominalPowerWhenReady() {
    var manufactory = CreateManufactory(
        nominalPower: 20,
        hasCurrentRecipe: true,
        hasAllIngredients: true,
        hasFuel: true,
        hasCapacity: true);

    Assert.Equal(20, manufactory.UpdateAndGetPowerInput());
    Assert.False(manufactory.StandbyMode);
  }

  public static void ZeroWithoutRecipe() {
    var manufactory = CreateManufactory(
        nominalPower: 20,
        hasCurrentRecipe: false,
        hasAllIngredients: false,
        hasFuel: false,
        hasCapacity: false);

    Assert.Equal(0, manufactory.UpdateAndGetPowerInput());
    Assert.False(manufactory.StandbyMode);
    Assert.False(manufactory.MissingIngredients);
    Assert.False(manufactory.NoFuel);
    Assert.False(manufactory.BlockedOutput);
  }

  static SmartManufactory CreateManufactory(
      int nominalPower,
      bool hasCurrentRecipe,
      bool hasAllIngredients,
      bool hasFuel,
      bool hasCapacity) {
    var manufactory = new SmartManufactory();
    manufactory.InjectDependencies(new FakeLoc());
    manufactory.SetComponent(new MechanicalNode { Active = true });
    manufactory.SetComponent(new MechanicalNodeSpec { PowerInput = nominalPower });
    manufactory.SetComponent(new Enterable { NumberOfEnterersInside = 1 });
    manufactory.SetComponent(new StatusSubject());
    manufactory.SetComponent(new Manufactory {
        HasCurrentRecipe = hasCurrentRecipe,
        HasAllIngredients = hasAllIngredients,
        HasFuel = hasFuel,
        HasUnreservedCapacity = hasCapacity,
    });
    manufactory.Awake();
    return manufactory;
  }
}

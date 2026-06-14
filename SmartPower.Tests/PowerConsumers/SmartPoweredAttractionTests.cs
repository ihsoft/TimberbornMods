using IgorZ.SmartPower.PowerConsumers;
using Timberborn.EnterableSystem;
using Timberborn.MechanicalSystem;

namespace SmartPower.Tests;

static class SmartPoweredAttractionTests {
  public static void LowersPowerWhenEmpty() {
    var attraction = CreateAttraction(10, enterers: 0);

    Assert.Equal(1, attraction.UpdateAndGetPowerInput());
  }

  public static void UsesNominalPowerWhenOccupied() {
    var attraction = CreateAttraction(10, enterers: 2);

    Assert.Equal(10, attraction.UpdateAndGetPowerInput());
  }

  public static void ReturnsZeroWhenInactive() {
    var attraction = CreateAttraction(10, enterers: 2, active: false);

    Assert.Equal(0, attraction.UpdateAndGetPowerInput());
  }

  static SmartPoweredAttraction CreateAttraction(int nominalPower, int enterers, bool active = true) {
    var attraction = new SmartPoweredAttraction();
    attraction.SetComponent(new MechanicalNode { Active = active });
    attraction.SetComponent(new MechanicalNodeSpec { PowerInput = nominalPower });
    attraction.SetComponent(new Enterable { NumberOfEnterersInside = enterers });
    attraction.Awake();
    return attraction;
  }
}

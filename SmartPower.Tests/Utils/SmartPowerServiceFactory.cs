using System;
using IgorZ.SmartPower.Core;
using Timberborn.TimeSystem;

namespace SmartPower.Tests;

static class SmartPowerServiceFactory {
  public static SmartPowerService Create(IDayNightCycle dayNightCycle) {
    var constructor = typeof(SmartPowerService).GetConstructor(
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
        null,
        [typeof(IDayNightCycle)],
        null);
    if (constructor == null) {
      throw new InvalidOperationException("SmartPowerService constructor was not found.");
    }
    return (SmartPowerService)constructor.Invoke([dayNightCycle]);
  }
}

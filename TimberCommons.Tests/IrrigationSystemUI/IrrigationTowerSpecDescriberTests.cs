using System.Linq;
using System.Reflection;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberCommons.IrrigationSystemUI;
using Timberborn.BlockSystem;

namespace TimberCommons.Tests;

static class IrrigationTowerSpecDescriberTests {
  public static void ShowsOnlyEfficiencyForUnfinishedTower() {
    var describer = CreateDescriber(isFinished: false, irrigationRange: 7);

    var description = describer.DescribeEntity().Single();

    Assert.Equal("● IgorZ.TimberCommons.IrrigationTower.Efficient", description.Content);
    Assert.Equal(2001, description.Order);
  }

  public static void ShowsRangeAndDistinctEffectsForFinishedTower() {
    var describer = CreateDescriber(isFinished: true, irrigationRange: 7);
    describer.AllComponents.Add(new ModifyGrowableGrowthRangeEffectSpec { GrowthRateModifier = 25 });
    describer.AllComponents.Add(new ModifyGrowableGrowthRangeEffectSpec { GrowthRateModifier = 25 });
    describer.AllComponents.Add(new ModifyGrowableGrowthRangeEffectSpec { GrowthRateModifier = -10 });
    describer.AllComponents.Add(new BlockContaminationRangeEffectSpec());

    var description = describer.DescribeEntity().Single();

    Assert.Equal(
        "● IgorZ.TimberCommons.IrrigationTower.Efficient\r\n"
        + "● IgorZ.TimberCommons.IrrigationTower.MaxRange:7\r\n"
        + "● IgorZ.TimberCommons.IrrigationTower.GrowthBoost:25\r\n"
        + "● IgorZ.TimberCommons.IrrigationTower.GrowthSlowdown:10\r\n"
        + "● IgorZ.TimberCommons.IrrigationTower.ContaminationBlock",
        description.Content);
  }

  static IrrigationTowerSpecDescriber CreateDescriber(bool isFinished, int irrigationRange) {
    var describer = (IrrigationTowerSpecDescriber)typeof(IrrigationTowerSpecDescriber)
        .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(FakeLoc)], null)
        .Invoke([new FakeLoc()]);
    describer.SetComponent(new BlockObject { IsFinished = isFinished });
    describer.SetComponent(new GoodConsumingIrrigationTowerSpec { IrrigationRange = irrigationRange });
    describer.Awake();
    return describer;
  }
}

using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.Growing;
using Timberborn.TimeSystem;
using UnityEngine;

namespace TimberCommons.Tests;

static class GrowthRateModifierTests {
  public static void CombinesModifiers() {
    var (modifier, growable, triggerFactory) = CreateModifier(originalGrowthTimeInDays: 10, progress: 0.25f);

    modifier.RegisterModifier("small boost", 20);
    modifier.RegisterModifier("best boost", 50);
    modifier.RegisterModifier("small moderator", -10);
    modifier.RegisterModifier("worst moderator", -30);

    Assert.Equal(50, modifier.BestBooster);
    Assert.Equal(-30, modifier.WorstModerator);
    Assert.Equal(20, modifier.EffectiveModifier);
    Assert.Equal(10 / 1.2f, growable._growableSpec.GrowthTimeInDays);
    Assert.Equal(4, triggerFactory.CreatedTriggers.Count);
    Assert.Equal(0.25f, triggerFactory.LastTrigger.FastForwardedProgress);
    Assert.True(triggerFactory.LastTrigger.Resumed);
  }

  public static void RecalculatesAfterUnregister() {
    var (modifier, growable, _) = CreateModifier(originalGrowthTimeInDays: 8, progress: 0.5f);

    modifier.RegisterModifier("boost", 60);
    modifier.RegisterModifier("moderator", -20);
    modifier.UnregisterModifier("moderator");

    Assert.Equal(60, modifier.EffectiveModifier);
    Assert.Equal(8 / 1.6f, growable._growableSpec.GrowthTimeInDays);
    Assert.True(modifier.RateIsModified);

    modifier.UnregisterModifier("boost");

    Assert.Equal(0, modifier.EffectiveModifier);
    Assert.Equal(8, growable._growableSpec.GrowthTimeInDays);
    Assert.False(modifier.RateIsModified);
  }

  public static void IgnoresInactiveGrowables() {
    var (modifier, growable, triggerFactory) = CreateModifier(originalGrowthTimeInDays: 10, progress: 0.25f);

    growable.IsGrown = true;
    modifier.RegisterModifier("boost", 50);

    Assert.Equal(0, modifier.EffectiveModifier);
    Assert.Equal(10, growable._growableSpec.GrowthTimeInDays);
    Assert.Equal(0, triggerFactory.CreatedTriggers.Count);
  }

  public static (GrowthRateModifier Modifier, Growable Growable, TestTimeTriggerFactory TriggerFactory) CreateModifier(
      float originalGrowthTimeInDays, float progress) {
    var growable = new Growable {
        GrowthProgress = progress,
        _growableSpec = new GrowableSpec { GrowthTimeInDays = originalGrowthTimeInDays },
        _timeTrigger = new TestTimeTrigger(),
    };
    var triggerFactory = new TestTimeTriggerFactory();
    var modifier = new GrowthRateModifier();
    modifier.SetComponent(growable);
    modifier.SetComponent(new GrowableSpec { GrowthTimeInDays = originalGrowthTimeInDays });
    modifier.InjectDependencies(triggerFactory);
    modifier.Awake();
    return (modifier, growable, triggerFactory);
  }
}

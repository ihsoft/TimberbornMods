using System.Collections.Generic;
using IgorZ.TimberCommons.IrrigationSystem;
using IgorZ.TimberCommons.WaterService;
using Timberborn.Persistence;
using UnityEngine;

namespace TimberCommons.Tests;

static class BlockContaminationRangeEffectTests {
  public static void AppliesAndReplacesContaminationOverride() {
    var service = new SoilOverridesService();
    var effect = CreateEffect(service);
    var firstTiles = new HashSet<Vector3Int> {
        new(1, 2, 0),
        new(2, 2, 0),
    };
    var secondTiles = new HashSet<Vector3Int> {
        new(3, 4, 0),
    };

    effect.ApplyEffect(firstTiles);
    effect.ApplyEffect(secondTiles);

    Assert.Equal(2, service.AddedContaminationOverrides.Count);
    Assert.Equal(1, service.RemovedContaminationOverrideIds.Count);
    Assert.Equal(1, service.RemovedContaminationOverrideIds[0]);
    Assert.Equal(2, service.ActiveContaminationOverrideId);
    Assert.Equal(1, service.ActiveContaminationOverrideTiles.Count);
    Assert.True(service.ActiveContaminationOverrideTiles.Contains(new Vector3Int(3, 4, 0)));

    effect.ResetEffect();
    effect.ResetEffect();

    Assert.Equal(-1, service.ActiveContaminationOverrideId);
    Assert.Equal(2, service.RemovedContaminationOverrideIds.Count);
    Assert.Equal(2, service.RemovedContaminationOverrideIds[1]);
  }

  public static void SavesAndClaimsLoadedOverride() {
    var service = new SoilOverridesService();
    var effect = CreateEffect(service);
    effect.ApplyEffect([new Vector3Int(1, 2, 0)]);
    var savedState = new EntityState();

    effect.Save(savedState);

    Assert.Equal(1, savedState.Component.Get<int>("OverrideIndex"));

    var loadedService = new SoilOverridesService();
    var loadedEffect = CreateEffect(loadedService);
    var loadedState = new EntityState();
    loadedState.Component.Set(new PropertyKey<int>("OverrideIndex"), 42);

    loadedEffect.Load(loadedState);

    Assert.Equal(42, loadedService.ClaimedContaminationOverrideIds[0]);
    loadedEffect.ResetEffect();
    Assert.Equal(42, loadedService.RemovedContaminationOverrideIds[0]);
  }

  static BlockContaminationRangeEffect CreateEffect(SoilOverridesService service) {
    var effect = new BlockContaminationRangeEffect();
    effect.SetComponent(new BlockContaminationRangeEffectSpec { EffectGroup = "contamination" });
    effect.InjectDependencies(service);
    effect.Awake();
    Assert.Equal("contamination", effect.EffectGroup);
    return effect;
  }
}

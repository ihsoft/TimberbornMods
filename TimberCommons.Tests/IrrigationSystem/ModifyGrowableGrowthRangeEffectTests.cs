using System.Collections.Generic;
using System.Collections.Immutable;
using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Growing;
using Timberborn.SingletonSystem;
using Timberborn.TemplateSystem;
using UnityEngine;

namespace TimberCommons.Tests;

static class ModifyGrowableGrowthRangeEffectTests {
  public static void AppliesAndResetsMatchingGrowables() {
    var tile = new Vector3Int(1, 2, 0);
    var blockService = new BlockService();
    var modifier = GrowthRateModifierTests.CreateModifier(originalGrowthTimeInDays: 10, progress: 0).Modifier;
    blockService.SetBottomObjectComponentAt(tile, modifier);
    var effect = CreateEffect(blockService, growthRateModifier: 100);

    effect.ApplyEffect([tile]);

    Assert.Equal(100, modifier.EffectiveModifier);
    Assert.Equal(5, modifier.GetComponent<Growable>()._growableSpec.GrowthTimeInDays);

    effect.ResetEffect();

    Assert.Equal(0, modifier.EffectiveModifier);
    Assert.Equal(10, modifier.GetComponent<Growable>()._growableSpec.GrowthTimeInDays);
  }

  public static void HonorsTemplateAndComponentFilters() {
    var matchingTile = new Vector3Int(1, 1, 0);
    var wrongTemplateTile = new Vector3Int(2, 1, 0);
    var missingComponentTile = new Vector3Int(3, 1, 0);
    var blockService = new BlockService();
    var matchingModifier = CreateGrowthModifier(templateName: "Oak", component: new MatchingComponent());
    var wrongTemplateModifier = CreateGrowthModifier(templateName: "Pine", component: new MatchingComponent());
    var missingComponentModifier = CreateGrowthModifier(templateName: "Oak");
    blockService.SetBottomObjectComponentAt(matchingTile, matchingModifier);
    blockService.SetBottomObjectComponentAt(wrongTemplateTile, wrongTemplateModifier);
    blockService.SetBottomObjectComponentAt(missingComponentTile, missingComponentModifier);
    var effect = CreateEffect(
        blockService,
        growthRateModifier: 50,
        componentsFilter: [typeof(MatchingComponent).FullName],
        templateNamesFilter: ["Oak"]);

    effect.ApplyEffect([matchingTile, wrongTemplateTile, missingComponentTile]);

    Assert.Equal(50, matchingModifier.EffectiveModifier);
    Assert.Equal(0, wrongTemplateModifier.EffectiveModifier);
    Assert.Equal(0, missingComponentModifier.EffectiveModifier);
  }

  public static void HandlesNewInitializedEntities() {
    var tile = new Vector3Int(1, 1, 0);
    var blockService = new BlockService();
    var effect = CreateEffect(blockService, growthRateModifier: 25);
    var modifier = CreateGrowthModifier();
    var entity = new BaseComponent();
    entity.SetComponent(modifier);
    modifier.SetComponent(new BlockObject { Coordinates = tile });

    effect.ApplyEffect([tile]);
    blockService.SetBottomObjectComponentAt(tile, modifier);
    effect.OnEntityInitializedEvent(new EntityInitializedEvent(entity));

    Assert.Equal(25, modifier.EffectiveModifier);
  }

  static GrowthRateModifier CreateGrowthModifier(string templateName = null, object component = null) {
    var modifier = GrowthRateModifierTests.CreateModifier(originalGrowthTimeInDays: 10, progress: 0).Modifier;
    if (templateName != null) {
      modifier.SetComponent(new TemplateSpec { TemplateName = templateName });
    }
    if (component != null) {
      modifier.SetComponent(component);
    }
    return modifier;
  }

  static ModifyGrowableGrowthRangeEffect CreateEffect(
      BlockService blockService,
      float growthRateModifier,
      ImmutableArray<string> componentsFilter = default,
      ImmutableArray<string> templateNamesFilter = default) {
    var effect = new ModifyGrowableGrowthRangeEffect();
    effect.SetComponent(new ModifyGrowableGrowthRangeEffectSpec {
        EffectGroup = "test",
        GrowthRateModifier = growthRateModifier,
        ComponentsFilter = componentsFilter.IsDefault ? [] : componentsFilter,
        TemplateNamesFilter = templateNamesFilter.IsDefault ? [] : templateNamesFilter,
    });
    effect.InjectDependencies(blockService, new EventBus());
    effect.Awake();
    return effect;
  }

  sealed class MatchingComponent {
  }
}

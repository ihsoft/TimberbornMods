using IgorZ.TimberCommons.WaterBuildings;
using System.Collections.Immutable;
using Timberborn.TemplateSystem;

namespace TimberCommons.Tests;

static class DischargeTemplateMatcherTests {
  public static void RecognizesCurrentDischargeTemplateNames() {
    Assert.True(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec { TemplateName = "Discharge.Folktails" }));
    Assert.True(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec { TemplateName = "Discharge.IronTeeth" }));
  }

  public static void RecognizesLegacyDischargeTemplateNamesThroughBackwardCompatibleAliases() {
    Assert.True(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec {
            TemplateName = "Discharge.Folktails",
            BackwardCompatibleTemplateNames = ImmutableArray.Create("FluidDump.Folktails"),
        }));
    Assert.True(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec {
            TemplateName = "Discharge.IronTeeth",
            BackwardCompatibleTemplateNames = ImmutableArray.Create("FluidDump.IronTeeth"),
        }));
  }

  public static void KeepsPumpTemplateNamesOutOfDischargeSettings() {
    Assert.False(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec { TemplateName = "MechanicalPump.Folktails" }));
    Assert.False(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec { TemplateName = "DeepMechanicalPump.IronTeeth" }));
    Assert.False(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec { TemplateName = "CompactMechanicalPump.Folktails" }));
    Assert.False(DischargeTemplateMatcher.UsesDischargeSettings(
        new TemplateSpec { TemplateName = "CompactMechanicalPump.IronTeeth" }));
    Assert.False(DischargeTemplateMatcher.UsesDischargeSettings(null));
  }
}

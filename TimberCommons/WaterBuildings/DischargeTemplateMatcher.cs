// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.TemplateSystem;

namespace IgorZ.TimberCommons.WaterBuildings;

/// <summary>
/// Keeps the Discharge template-name migration check in one tested place while Timberborn still exposes FluidDump
/// backward-compatible aliases.
/// </summary>
static class DischargeTemplateMatcher {
  const string DischargeFolktailsTemplateName = "Discharge.Folktails";
  const string DischargeIronTeethTemplateName = "Discharge.IronTeeth";
  // FIXME: Remove these legacy aliases when Timberborn drops the FluidDump backward-compatible template names.
  const string FluidDumpFolktailsTemplateName = "FluidDump.Folktails";
  const string FluidDumpIronTeethTemplateName = "FluidDump.IronTeeth";

  internal static bool UsesDischargeSettings(TemplateSpec templateSpec) {
    return templateSpec != null
        && (templateSpec.IsNamed(DischargeFolktailsTemplateName)
            || templateSpec.IsNamed(DischargeIronTeethTemplateName)
            || templateSpec.IsNamed(FluidDumpFolktailsTemplateName)
            || templateSpec.IsNamed(FluidDumpIronTeethTemplateName));
  }
}

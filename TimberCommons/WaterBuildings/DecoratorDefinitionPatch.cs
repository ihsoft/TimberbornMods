// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using HarmonyLib;
using Timberborn.TemplateInstantiation;
using Timberborn.WaterBuildings;

namespace IgorZ.TimberCommons.WaterBuildings;

// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming

[HarmonyPatch(typeof(DecoratorDefinition), nameof(DecoratorDefinition.CreateSingleton))]
static class DecoratorDefinitionPatch {
  static void Prefix(bool __runOriginal, ref Type decoratorType) {
    if (!__runOriginal) {
      return;  // The other patches must follow the same style to properly support the skip logic!
    }
    if (decoratorType == typeof(WaterOutput)) {
      decoratorType = typeof(AdjustableWaterOutput);
    }
  }
}

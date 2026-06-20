// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Timberborn.AssetSystem;
using Timberborn.Localization;
using Timberborn.UIFormatters;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberCommons.IrrigationSystemUI;

sealed class IrrigationTowerOutputFactory {
  const string IrrigationRangeAmountLocKey = "IgorZ.TimberCommons.IrrigationTower.OutputRangeAmount";
  const string IrrigationRangeTooltipLocKey = "IgorZ.TimberCommons.IrrigationTower.OutputRangeTooltip";
  const string GrowthModifierAmountLocKey = "IgorZ.TimberCommons.IrrigationTower.OutputGrowthModifierAmount";
  const string GrowthModifierTooltipLocKey = "IgorZ.TimberCommons.IrrigationTower.OutputGrowthModifierTooltip";
  const string ContaminationBlockTooltipLocKey = "IgorZ.TimberCommons.IrrigationTower.OutputContaminationBlockTooltip";
  const string IrrigationRangeIconPath = "Sprites/IgorZ.TimberCommons/Recipes/ico-radius";
  const string GrowthModifierIconPath = "Sprites/IgorZ.TimberCommons/Recipes/ico-growth";
  const string ContaminationBlockIconPath = "Sprites/IgorZ.TimberCommons/Recipes/ico-block-contamination";

  readonly ILoc _loc;
  readonly DescribedAmountFactory _describedAmountFactory;
  readonly IAssetLoader _assetLoader;
  Sprite _irrigationRangeIcon;
  Sprite _growthModifierIcon;
  Sprite _contaminationBlockIcon;

  IrrigationTowerOutputFactory(ILoc loc, DescribedAmountFactory describedAmountFactory, IAssetLoader assetLoader) {
    _loc = loc;
    _describedAmountFactory = describedAmountFactory;
    _assetLoader = assetLoader;
  }

  public VisualElement CreateIrrigationRangeOutput(int irrigationRange) {
    var amount = _loc.T(IrrigationRangeAmountLocKey, irrigationRange);
    var tooltip = _loc.T(IrrigationRangeTooltipLocKey, irrigationRange);
    return _describedAmountFactory.CreatePlain("", amount, GetIrrigationRangeIcon(), tooltip);
  }

  public VisualElement CreateGrowthModifierOutput(float growthRateModifier) {
    var amount = _loc.T(GrowthModifierAmountLocKey, growthRateModifier);
    var tooltip = _loc.T(GrowthModifierTooltipLocKey);
    return _describedAmountFactory.CreatePlain("", amount, GetGrowthModifierIcon(), tooltip);
  }

  public VisualElement CreateContaminationBlockOutput() {
    var tooltip = _loc.T(ContaminationBlockTooltipLocKey);
    return _describedAmountFactory.CreatePlain("", "", GetContaminationBlockIcon(), tooltip);
  }

  Sprite GetIrrigationRangeIcon() {
    return _irrigationRangeIcon ??= _assetLoader.Load<Sprite>(IrrigationRangeIconPath);
  }

  Sprite GetGrowthModifierIcon() {
    return _growthModifierIcon ??= _assetLoader.Load<Sprite>(GrowthModifierIconPath);
  }

  Sprite GetContaminationBlockIcon() {
    return _contaminationBlockIcon ??= _assetLoader.Load<Sprite>(ContaminationBlockIconPath);
  }
}

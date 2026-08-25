// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Collections.Generic;
using System.Linq;
using System.Text;
using IgorZ.TimberCommons.IrrigationSystem;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Common;
using Timberborn.CoreUI;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;

namespace IgorZ.TimberCommons.IrrigationSystemUI;

sealed class IrrigationTowerSpecDescriber : BaseComponent, IAwakableComponent, IEntityDescriber {
  const string MaxRangeLocKey = "IgorZ.TimberCommons.IrrigationTower.MaxRange";
  const string EfficientLocKey = "IgorZ.TimberCommons.IrrigationTower.Efficient";
  const string GrowthBoostLocKey = "IgorZ.TimberCommons.IrrigationTower.GrowthBoost";
  const string GrowthSlowdownLocKey = "IgorZ.TimberCommons.IrrigationTower.GrowthSlowdown";
  const string ContaminationBlockLocKey = "IgorZ.TimberCommons.IrrigationTower.ContaminationBlock";
  const int DescriptionOrder = 2001;

  readonly ILoc _loc;
  readonly StringBuilder _descriptionBuilder = new();
  BlockObject _blockObject;
  bool _showDetailsWhenFinished;
  int _irrigationRange;

  IrrigationTowerSpecDescriber(ILoc loc) {
    _loc = loc;
  }

  public void Awake() {
    _blockObject = GetComponent<BlockObject>();
    var goodConsumingSpec = GetComponent<GoodConsumingIrrigationTowerSpec>();
    if (goodConsumingSpec != null) {
      _showDetailsWhenFinished = true;
      _irrigationRange = goodConsumingSpec.IrrigationRange;
    }
  }

  public IEnumerable<EntityDescription> DescribeEntity() {
    _descriptionBuilder.Clear();
    _descriptionBuilder.AppendLine(SpecialStrings.RowStarter + _loc.T(EfficientLocKey));
    if (_showDetailsWhenFinished && _blockObject.IsFinished) {
      _descriptionBuilder.AppendLine(SpecialStrings.RowStarter + _loc.T(MaxRangeLocKey, _irrigationRange));
      AddEffects();
    }
    yield return EntityDescription.CreateTextSection(_descriptionBuilder.ToStringWithoutNewLineEnd(), DescriptionOrder);
  }

  void AddEffects() {
    var growthEffects = new List<ModifyGrowableGrowthRangeEffectSpec>();
    GetComponents(growthEffects);
    var contaminationEffects = new List<BlockContaminationRangeEffectSpec>();
    GetComponents(contaminationEffects);
    var effects = growthEffects
        .Select(effect => (
            effect.EffectGroup,
            Description: DescribeGrowthEffect(effect.GrowthRateModifier)))
        .Concat(contaminationEffects.Select(effect => (
            effect.EffectGroup,
            Description: _loc.T(ContaminationBlockLocKey))))
        .ToList();
    if (effects.Count == 0) {
      return;
    }

    foreach (var effect in effects.Select(effect => effect.Description).Distinct()) {
      _descriptionBuilder.AppendLine(SpecialStrings.RowStarter + effect);
    }
  }

  string DescribeGrowthEffect(float growthRateModifier) {
    return growthRateModifier >= 0
        ? _loc.T(GrowthBoostLocKey, growthRateModifier)
        : _loc.T(GrowthSlowdownLocKey, -growthRateModifier);
  }
}

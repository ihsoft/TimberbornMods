// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using IgorZ.Automation.AutomationSystem;
using Timberborn.Persistence;
using Timberborn.WaterSourceSystem;

namespace IgorZ.Automation.Actions;

/// <summary>Opens or closes water discharges.</summary>
// ReSharper disable once UnusedType.Global
public class WaterRegulatorStateAction : AutomationActionBase {
  const string OpenDescriptionLocKey = "IgorZ.Automation.WaterRegulatorStateActionOpen.Description";
  const string CloseDescriptionLocKey = "IgorZ.Automation.WaterRegulatorStateActionClose.Description";

  /// <summary>Number of 0.5m steps down from the maximum floodgate height.</summary>
  // ReSharper disable once MemberCanBePrivate.Global
  public bool Open { get; private set; }

  #region AutomationActionBase overrides

  /// <inheritdoc/>
  public override string UiDescription => Behavior.Loc.T(Open ? OpenDescriptionLocKey : CloseDescriptionLocKey);

  /// <inheritdoc/>
  public override IAutomationAction CloneDefinition() {
    return new WaterRegulatorStateAction { TemplateFamily = TemplateFamily, Open = Open };
  }

  /// <inheritdoc/>
  public override bool IsValidAt(AutomationBehavior behavior) {
    return behavior.GetComponentFast<WaterSourceRegulator>();
  }

  /// <inheritdoc/>
  public override void OnConditionState(IAutomationCondition automationCondition) {
    if (!Condition.ConditionState) {
      return;
    }
    var regulator = Behavior.GetComponentFast<WaterSourceRegulator>();
    if (Open) {
      regulator.Open();
    } else {
      regulator.Close();
    }
  }
  #endregion

  #region IGameSerializable implemenation
  static readonly PropertyKey<bool> OpenPropertyKey = new("Open");

  /// <summary>Loads action state and declaration.</summary>
  public override void LoadFrom(IObjectLoader objectLoader) {
    base.LoadFrom(objectLoader);
    Open = objectLoader.Get(OpenPropertyKey);
  }

  /// <summary>Saves action state and declaration.</summary>
  public override void SaveTo(IObjectSaver objectSaver) {
    base.SaveTo(objectSaver);
    objectSaver.Set(OpenPropertyKey, Open);
  }
  #endregion
}
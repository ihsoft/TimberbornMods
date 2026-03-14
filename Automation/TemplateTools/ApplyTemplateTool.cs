// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

extern alias CustomTools;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using IgorZ.Automation.Actions;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.Conditions;
using IgorZ.Automation.Utils;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.ConstructionMode;
using Timberborn.Persistence;
using UnityEngine;
using AbstractAreaSelectionTool = CustomTools::IgorZ.CustomTools.Tools.AbstractAreaSelectionTool;

namespace IgorZ.Automation.TemplateTools;

// ReSharper disable once ClassNeverInstantiated.Global
sealed class ApplyTemplateTool : AbstractAreaSelectionTool, IAutomationModeEnabler, IConstructionModeEnabler {

  static readonly Color ToolColor = new(0, 1, 1, 0.7f);

  #region Tool spec

  /// <summary>Spec that holds the template tool configuration.</summary>
  public sealed record AutomationTemplateSpec : ComponentSpec {
    [Serialize]
    public string TemplateFamilyName { get; init; } = "";

    public record DynamicTypeSpec {
      [Serialize]
      public string TypeId { get; init; }

      [Serialize]
      public ImmutableArray<SpecToSaveObjectConverter.AutomationParameterSpec> Parameters { get; init; }
    }

    public record AutomationRuleSpec {
      [Serialize]
      public DynamicTypeSpec Condition { get; init; }

      [Serialize]
      public DynamicTypeSpec Action { get; init; }
    }

    [Serialize]
    public ImmutableArray<AutomationRuleSpec> Rules { get; init; }
  }

  #endregion

  #region AbstractAreaSelectionTool overries

  /// <inheritdoc/>
  protected override string CursorName => "AutomationCogCursor";

  /// <inheritdoc/>
  protected override bool ObjectFilterExpression(BlockObject blockObject) {
    var behavior = blockObject.GetComponent<AutomationBehavior>();
    if (!behavior || !behavior.Enabled) {
      return false;
    }
    return _templateRules.All(rule => rule.IsValidAt(behavior));
  }

  /// <inheritdoc/>
  protected override void OnObjectAction(BlockObject blockObject) {
    var behavior = blockObject.GetComponent<AutomationBehavior>();
    behavior.RemoveRulesForTemplateFamily(_templateFamilyName);
    foreach (var rule in _templateRules) {
      var action = rule.Action.CloneDefinition();
      action.TemplateFamily = _templateFamilyName;
      behavior.AddRule(rule.Condition.CloneDefinition(), action);
    }
  }

  /// <inheritdoc/>
  protected override void Initialize() {
    SetColorSchema(ToolColor, ToolColor, Color.cyan, Color.cyan);
    base.Initialize();
    DeserializeSpec();
  }

  #endregion

  #region Implementation

  string _templateFamilyName;
  readonly List<AutomationRule> _templateRules = [];

  void DeserializeSpec() {
    var templateSpec = ToolSpec.GetSpec<AutomationTemplateSpec>();
    _templateFamilyName = templateSpec.TemplateFamilyName;
    foreach (var rule in templateSpec.Rules) {
      var condition = ParseAndInit<AutomationConditionBase>(rule.Condition);
      var action = ParseAndInit<AutomationActionBase>(rule.Action);
      _templateRules.Add(new AutomationRule(condition, action));
    }
  }

  static T ParseAndInit<T>(AutomationTemplateSpec.DynamicTypeSpec typeSpec) where T : class, IGameSerializable {
    var instance = ReflectionsHelper.MakeInstance<T>(typeSpec.TypeId);
    if (typeSpec.Parameters != null && typeSpec.Parameters.Length > 0) {
      instance.LoadFrom(new ObjectLoader(SpecToSaveObjectConverter.ParametersToSaveObject(typeSpec.Parameters)));
    }
    return instance;
  }

  #endregion
}
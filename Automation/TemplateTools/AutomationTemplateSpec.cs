using System.Collections.Immutable;
using IgorZ.Automation.Utils;
using Timberborn.BlueprintSystem;

namespace IgorZ.Automation.TemplateTools;

/// <summary>Spec that holds the template tool configuration.</summary>
sealed record AutomationTemplateSpec : ComponentSpec {
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

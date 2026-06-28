using System.Collections.Immutable;
using Timberborn.BaseComponentSystem;

namespace Timberborn.TemplateSystem;

public sealed class TemplateSpec : BaseComponent {
  public string TemplateName { get; init; }
  public ImmutableArray<string> BackwardCompatibleTemplateNames { get; init; } = ImmutableArray<string>.Empty;

  public bool IsNamed(string templateName) {
    if (IsNamedExactly(templateName)) {
      return true;
    }
    foreach (var backwardCompatibleTemplateName in BackwardCompatibleTemplateNames) {
      if (backwardCompatibleTemplateName == templateName) {
        return true;
      }
    }
    return false;
  }

  public bool IsNamedExactly(string templateName) {
    return TemplateName == templateName;
  }
}

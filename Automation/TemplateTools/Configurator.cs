// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.Automation.TemplateTools;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {

  /// <summary>The tool group ID to bind all the automation tools.</summary>
  const string AutomationTooGroupId = "AutomationToolGroup";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<ApplyTemplateTool.AutomationTemplateSpec>().AsTransient();
    containerDefinition.Bind<ApplyTemplateTool>().AsTransient();
  }
}

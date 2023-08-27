// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Templates;
using Automation.Tools;
using Automation.Utils;
using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;
using Timberborn.Buildings;
using Timberborn.TemplateSystem;

namespace Automation.Core {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    CustomToolSystem.BindGroupWithConstructionModeEnabler(containerDefinition, "AutomationToolGroup");
    CustomToolSystem.BindTool<PauseTool>(containerDefinition);
    CustomToolSystem.BindTool<ResumeTool>(containerDefinition);
    CustomToolSystem.BindTool<CancelTool>(containerDefinition);
    CustomToolSystem.BindTool<ApplyTemplateTool, ApplyTemplateTool.ToolInfo>(containerDefinition);
    CustomToolSystem.BindTool<DebugPickTool>(containerDefinition);
    containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
    containerDefinition.Bind<AutomationService>().AsSingleton();
  }

  static TemplateModule ProvideTemplateModule() {
    var builder = new TemplateModule.Builder();
    builder.AddDecorator<Building, AutomationBehavior>();
    return builder.Build();
  }
}

}

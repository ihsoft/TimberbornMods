// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Automation.Utils;
using Bindito.Core;
using TimberApi.ConfiguratorSystem;
using TimberApi.SceneSystem;

namespace Automation.Tools {

[Configurator(SceneEntrypoint.InGame)]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {

  /// <summary>The tool group ID to bind all the automation tools.</summary>
  const string AutomationTooGroupId = "AutomationToolGroup";

  public void Configure(IContainerDefinition containerDefinition) {
    CustomToolSystem.BindGroupWithConstructionModeEnabler(containerDefinition, AutomationTooGroupId);
    CustomToolSystem.BindTool<PauseTool>(containerDefinition);
    CustomToolSystem.BindTool<ResumeTool>(containerDefinition);
    CustomToolSystem.BindTool<CancelTool>(containerDefinition);
    CustomToolSystem.BindTool<ApplyTemplateTool, ApplyTemplateTool.ToolInfo>(containerDefinition);
    CustomToolSystem.BindTool<DebugPickTool>(containerDefinition);
    CustomToolSystem.BindTool<DebugFinishNowTool>(containerDefinition);
  }
}

}

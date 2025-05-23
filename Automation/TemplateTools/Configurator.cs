﻿// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using IgorZ.TimberDev.Tools;

namespace IgorZ.Automation.TemplateTools;

[Context("Game")]
[Context("MapEditor")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {

  /// <summary>The tool group ID to bind all the automation tools.</summary>
  const string AutomationTooGroupId = "AutomationToolGroup";

  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<ApplyTemplateTool.AutomationTemplateSpec>().AsTransient();
    CustomToolSystem.BindTool<ApplyTemplateTool>(containerDefinition);
  }
}

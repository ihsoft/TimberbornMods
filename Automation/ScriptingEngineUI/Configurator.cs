// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.Automation.ScriptingEngineUI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    containerDefinition.Bind<ConstructorEditorProvider>().AsSingleton();
    containerDefinition.Bind<ExportRulesDialog>().AsSingleton();
    containerDefinition.Bind<ImportRulesDialog>().AsSingleton();
    containerDefinition.Bind<RulesEditorDialog>().AsSingleton();
    containerDefinition.Bind<ScriptEditorProvider>().AsSingleton();
    containerDefinition.Bind<RulesUIHelper>().AsTransient();
    containerDefinition.Bind<SignalsEditorDialog>().AsSingleton();
  }
}

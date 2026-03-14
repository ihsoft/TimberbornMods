// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;

namespace IgorZ.Automation.ScriptingEngineUI;

[Context("Game")]
// ReSharper disable once UnusedType.Global
sealed class Configurator : IConfigurator {
  public void Configure(IContainerDefinition containerDefinition) {
    // Keep the order. It defines how the buttons are shown in UI.
    containerDefinition.MultiBind<IEditorButtonProvider>().To<ScriptEditorButtonProvider>().AsSingleton();
    containerDefinition.MultiBind<IEditorButtonProvider>().To<ConstructorEditorButtonProvider>().AsSingleton();
    containerDefinition.MultiBind<IEditorButtonProvider>().To<CopyRuleButtonProvider>().AsSingleton();
    containerDefinition.MultiBind<IEditorButtonProvider>().To<InvertRuleButtonProvider>().AsSingleton();

    containerDefinition.Bind<ExportRulesDialog>().AsTransient();
    containerDefinition.Bind<ExpressionDescriber>().AsSingleton();
    containerDefinition.Bind<ImportRulesDialog>().AsTransient();
    containerDefinition.Bind<RuleRow>().AsTransient();
    containerDefinition.Bind<RulesEditorDialog>().AsTransient();
    containerDefinition.Bind<SignalsEditorDialog>().AsTransient();
  }
}

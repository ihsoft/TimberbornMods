// Timberborn Mod: SmartPower
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

// This is an example file for registering the DebugSettings class in the Timberborn modding system.
// Include DebugSettings in to your project and add the following code to your mod's configurator file.
// Remember to copy over the localization files into your mod assets folder!

// using Bindito.Core;
// using IgorZ.TimberDev.Settings;
// using ModSettings.Core;
// using Timberborn.Modding;
// using Timberborn.SettingsSystem;
//
// // ReSharper disable once CheckNamespace
// namespace IgorZ.TimberDev.Settings;
//
// [Context("MainMenu")]
// [Context("Game")]
// sealed class Configurator : IConfigurator {
//   const string AutomationModId = "Timberborn.Author.ModName";
//
//   public void Configure(IContainerDefinition containerDefinition) {
//     containerDefinition.Bind<MyModDebugSettings>().AsSingleton();
//   }
//
//   sealed class MyModDebugSettings : DebugSettings {
//     protected override string ModId => AutomationModId;
//
//     // Additional debug settings can be added here
//     public ModSetting<bool> DebugOption1 { get; } = new(false, ModSettingDescriptor.Create("DebugOption1"));
//
//     AutomationDebugSettings(
//         ISettings settings, ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
//         : base(settings, modSettingsOwnerRegistry, modRepository) {
//     }
//   }
// }

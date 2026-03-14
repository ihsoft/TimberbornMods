# Timberborn: Custom Tools

This mod offers new quality-of-life tools in the bottom bar and also provides an API for modders to quickly create their own tools.

## For the players: Built-in tools

![Custom tools bottom bar button](https://raw.githubusercontent.com/ihsoft/TimberbornMods/refs/heads/timberborn-1.0/CustomTools/Workshop/Showcase/GroupButtonDemo.png)

### Immediate finish of incomplete buildings

This tool is only visible when the dev mode is activated (Shift + Alt + Z). Select multiple incomplete buildings and have
them instantly completed. A handy tool when testing or prototyping.

![Immediate finish of incomplete buildings](https://raw.githubusercontent.com/ihsoft/TimberbornMods/refs/heads/timberborn-1.0/CustomTools/Workshop/Showcase/FinishNowToolDemo.png)

### Pause buildings in the selected range

Select multiple buildings that need to be paused. Hold **SHIFT** to lock the selection to a specific building type.  
A useful tool for temporarily stopping groups of buildings during colony micromanagement.

![Pause buildings in the selected range](https://raw.githubusercontent.com/ihsoft/TimberbornMods/refs/heads/timberborn-1.0/CustomTools/Workshop/Showcase/PauseBuildingToolDemo.png)

### Resume buildings in the selected range

Select multiple buildings you want to resume. Hold **SHIFT** to lock the selection to a specific building type.  
Helps quickly bring groups of buildings back online during micromanagement.

![Resume buildings in the selected range](https://raw.githubusercontent.com/ihsoft/TimberbornMods/refs/heads/timberborn-1.0/CustomTools/Workshop/Showcase/ResumeBuildingToolDemo.png)

## For the modders: Simplified API to create your own tools

This mod is based on the [`Moddable Groups`](https://steamcommunity.com/sharedfiles/filedetails/?id=3612110827) mod.
You can choose to use that mod for the maximum flexibility in configuring tools. However, if your mod only needs “a
button” or a small set of buttons, you can use this mod to set up your tools with minimum coding efforts:

1. Create a simple class that inherits from
   [`AbstractCustomTool`](https://github.com/ihsoft/TimberbornMods/blob/timberborn-1.0/CustomTools/Tools/AbstractCustomTool.cs)
   or one of its descendants. This class will be serving the tool functionality.
   * This class must be bound via Bindito: `containerDefinition.Bind<PauseTool>().AsSingleton()`.
   * If you plan to use the same class for serving multiple tools, bind as transient:
     `containerDefinition.Bind<PauseTool>().AsTransient()`.

2. Create a blueprint that defines the appearance of your tool button. See example blueprint:
   [`DebugFinishNowTool`](https://github.com/ihsoft/TimberbornMods/blob/timberborn-1.0/CustomTools/Mod/Blueprints/Tools/Tool.CustomTools.DebugFinishNowTool.blueprint.json).
   * The file name _must_ follow the blueprint naming convention: `<AnyArbitraryText>.blueprint.json`.
   * Blueprint file names must be __globally unique__. The subfolders are not counted!
   * Add [`CustomToolSpec`](https://github.com/ihsoft/TimberbornMods/blob/8704467e2e08885f47f8b4cce06ed01912e48672/CustomTools/Core/CustomToolSpec.cs)
     and set `GroupId` to the name of the relevant group. The name of the standard _CustomTools_ tool group is
     "CustomToolsToolGroup", so you can put your tools there. Or you can define your own group (see below).
   * You can add additional specs to then tool blueprint to control behavior or provide extra data to your class. In the
     tool implementation, get extra specs via `ToolSpec.GetSpec<MyExtraSpec>()`.

3. __Optional__. Create your own group button in the bottom bar and attach your tools to it. See example blueprint:
   [`ToolGroup.CustomTools`](https://github.com/ihsoft/TimberbornMods/blob/8704467e2e08885f47f8b4cce06ed01912e48672/CustomTools/Mod/Blueprints/ToolGroups/ToolGroup.CustomTools.blueprint.json).
   * The file name _must_ follow the blueprint naming convention: `<AnyArbitraryText>.blueprint.json`.
   * Blueprint file names must be __globally unique__. The subfolders are not counted!
   * Groups can be nested. Set `CustomGroupSpec.ParentGroupId` to a name of another groups, and it will become a
     subgroup.
   * In subgroups, value of `CustomGroupSpec.Layout` is ignored. And `CustomGroupSpec.Order` is considered together with
     the tools in the same parent group. I.e. the groups and tools can be mixed in any combinations (based on the order).

### Tool examples

* [`DebugFinishNowTool`](https://github.com/ihsoft/TimberbornMods/blob/timberborn-1.0/CustomTools/Tools/DebugFinishNowTool.cs).
  A basic tool that selects a set of block objects on the map and performs actions on them.
  Its blueprint can be found [here](https://github.com/ihsoft/TimberbornMods/blob/8704467e2e08885f47f8b4cce06ed01912e48672/CustomTools/Mod/Blueprints/Tools/Tool.CustomTools.DebugFinishNowTool.blueprint.json).

* [`PauseTool`](https://github.com/ihsoft/TimberbornMods/blob/timberborn-1.0/CustomTools/Tools/PauseTool.cs).
  A more advanced example that uses selection locking to target specific object types.

Simple tool to gather _all_ dependencies from C# assembly DLL.

Point the tool to any DLL inside the game folder. All the requeried depecnencies will be either
printed or copied to the designated folder.

E.g. in order to be able making custom cursor resources for `CursorService.SetCursor`, you'd need
to import into your Unity project `Timberborn.InputSystem.dll` and _all_ its direct and indirect
dependecies.

```
$ CollectDependencies u:\Timberborn_Data\Managed\Timberborn.InputSystem.dll
$ CollectDependencies u:\Timberborn_Data\Managed\Timberborn.InputSystem.dll k:\TimberUnityProject\Assets\Scripts
```

HINT. When importing assemblies to Unity, do it while the editor is not loaded. Otherwise, the editor may hang.

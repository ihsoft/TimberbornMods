# SmartPower Agent Notes

These notes apply only when working inside the SmartPower mod.

## Compatibility-Line Ownership

SmartPower has separately maintained compatibility lines:

- the registered `update-1.0` worktree owns Timberborn Update 1.0 source and the `version-1.0` package lane;
- the main worktree owns current Timberborn Update 1.1 source and the `version-1.1` package lane.

The first committed Update 1.0 backport baseline is `c0e2ff3e34b6073ad4357d8f92801b90bea06eee` on
`update-1.0`. That worktree uses the same relative `SmartPower/SmartPower.csproj`,
`SmartPower.Tests/SmartPower.Tests.csproj`, and `ModsUnityProject/Assets/Mods/SmartPower` owners with its Timberborn
1.0 assemblies, Unity project, and `version-1.0` package output. Do not use current-line assemblies, generated
references, Unity exports, or package output as evidence for the older line.

Backports remain explicit user-opt-in work and advance patch-only from the latest dated SmartPower release on that
line. Use line-qualified tags such as `SmartPower_<Version>_update-1.0` for older-line commits; current-line commits use
ordinary unqualified tags. When both lines change in one public package, both source commits receive tags and the
current-line tag is primary for the GitHub Release.

## Build And Export

For Update 1.0 work, use the registered compatibility worktree, Timberborn 1.0 dependencies, and its matching Unity
editor and project. Export `version-1.0` first, then build `SmartPower/SmartPower.csproj` into that same package. After
the user confirms real-game behavior, run `dotnet run --project SmartPower.Tests/SmartPower.Tests.csproj` in the same
worktree and verify the final package contains both Unity-owned data and the rebuilt DLL/XML layer.

Do not rebuild an unchanged compatibility lane from the other line. Combine independently validated lanes only through
the immutable release and recovery workflows, preserving each lane fingerprint and manifest version.

## Settings Compatibility

Keep the PlayerPrefs identity of settings shared between the Update 1.0 and Update 1.1 lines when the setting has the
same public meaning. The mod ID, settings owner, and public property names intentionally remain identical so a player
moving between supported game lines keeps the same SmartPower preferences. Rename one of those identity components
only as an intentional settings migration with explicit compatibility handling.

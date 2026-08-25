# SmartPower Agent Notes

These notes apply only when working inside SmartPower on the `update-1.0` compatibility branch.

## Compatibility-Line Ownership

This worktree owns SmartPower's Timberborn Update 1.0 source and `version-1.0` package lane. The first committed
backport baseline is `c0e2ff3e34b6073ad4357d8f92801b90bea06eee`.

Use the relative owners in this worktree:

- production: `SmartPower/SmartPower.csproj`;
- focused tests: `dotnet run --project SmartPower.Tests/SmartPower.Tests.csproj`;
- Unity data: `ModsUnityProject/Assets/Mods/SmartPower`;
- package output: `SmartPower/version-1.0` under the selected old-game local mod root.

Build against the configured Timberborn 1.0 installation and this worktree's Unity project and editor. Do not use the
main worktree's Update 1.1 assemblies, generated references, Unity export, or package as evidence for this line.

## Version And Release

Backport releases advance patch-only from the latest dated release in this branch's SmartPower changelog. Features do
not promote the older line to a new minor version. Use the qualified tag `SmartPower_<Version>_update-1.0`; the
unqualified tag is reserved for the current line.

Use current provenance-capable release and generated-reference tooling from the main tooling checkout with this
worktree as its explicit target root. A changed Update 1.0 lane must receive its own immutable preflight before it is
combined with the current lane. When both lines change, both source commits receive tags and the current-line tag is
primary for the GitHub Release.

## Build And Export

Export `version-1.0` through this worktree's Unity project first, then build `SmartPower/SmartPower.csproj` into the
same package. Verify the final package contains the Unity layer and `Scripts/SmartPower.dll` plus
`Scripts/SmartPower.xml` before asking the user to test in Timberborn 1.0. After user confirmation, run the focused
test project in this worktree.

## Settings Compatibility

The Update 1.0 and Update 1.1 lines intentionally share PlayerPrefs setting identity through the same mod ID, settings
owner, and public property names. Preserve those identifiers for settings with the same meaning across both lines.

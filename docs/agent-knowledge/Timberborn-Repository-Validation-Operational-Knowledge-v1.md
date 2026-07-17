# Timberborn Repository Validation Operational Knowledge

## Purpose

Provide package-specific build, export, real-game output, and focused test selection for ordinary TimberbornMods
implementation and validation work.

This document does not replace:

- repository notes for architecture, project roles, generated references, changelogs, compatibility policy, or history;
- Unity Operational Knowledge for Unity artifact ownership and export boundaries;
- the root real-game validation gate;
- release rules for release-specific build ordering, package sources, or publication.

## Package Build And Validation Matrix

Update this matrix when a production project, test project, package-data location, or local `AGENTS.md` is added,
removed, or renamed. Adding a new mod is not complete until its row records the known project, focused test status,
package-data ownership, and local instructions.

Focused test projects are executable console runners. Run them with `dotnet run --project`, not `dotnet test`.
`No focused tests` means no package-specific test project exists in the current repository; it does not mean that
testing is unnecessary or that an unrelated package's tests should be substituted.

| Scope | Production project | Focused validation after real-game confirmation | Package data | Extra instructions |
|---|---|---|---|---|
| Automation | `Automation/Automation.csproj` | `dotnet run --project Automation.Tests/Automation.Tests.csproj` | `ModsUnityProject/Assets/Mods/Automation` | `Automation/AGENTS.md` |
| AutomationForModdableWeather | `AutomationForModdableWeather/AutomationForModdableWeather.csproj` | No focused tests | Project-local manifest and content | None |
| CustomTools | `CustomTools/CustomTools.csproj` | `dotnet run --project CustomTools.Tests/CustomTools.Tests.csproj` | `ModsUnityProject/Assets/Mods/CustomTools` | None |
| SmartHaulers | `SmartHaulers/SmartHaulers.csproj` | No focused tests | Split between `SmartHaulers/Mod` and `ModsUnityProject/Assets/Mods/SmartHaulers` | `SmartHaulers/AGENTS.md` |
| SmartPower | `SmartPower/SmartPower.csproj` | `dotnet run --project SmartPower.Tests/SmartPower.Tests.csproj` | `ModsUnityProject/Assets/Mods/SmartPower` | None |
| TimberCommons | `TimberCommons/TimberCommons.csproj` | `dotnet run --project TimberCommons.Tests/TimberCommons.Tests.csproj` | `ModsUnityProject/Assets/Mods/TimberCommons` | `docs/TimberCommons-modding-notes-for-ai-agents.md` |
| TimberDev source | No standalone production project in the current checkout | `dotnet run --project TimberDev.Tests/TimberDev.Tests.csproj` | No standalone mod package | `docs/timberborn-repository-notes.md` |
| XRay | `XRay/XRay.csproj` | `dotnet run --project XRay.Tests/XRay.Tests.csproj` | `ModsUnityProject/Assets/Mods/XRay` | `XRay/AGENTS.md` |
| TimberUI | Reference only; do not build by default | None | `ModsUnityProject/Assets/Mods/TimberUI` | `docs/timberborn-repository-notes.md` |

`CustomResources`, `TestParser`, `TestSupport`, `UnityDevLite`, and projects under `tools/` are not active mod packages
in this matrix. Inspect their specific project purpose before applying a mod build, export, test, or release workflow.

## Bulk Package Selection

Before a repository-wide or pattern-based build, export, or validation operation, collect the matching paths and then
classify their owning packages using the matrix and closest local instructions. A package does not become active merely
because it contains files matching the requested pattern.

Exclude reference-only, retired, dead, example-only, and otherwise inactive packages from bulk operations unless the
user explicitly names that package and asks to build, export, validate, or revive it. A broad request such as "all mods
with UXML" means all active mod packages matching the pattern; it does not override an inactive package status.

If a matching package's status cannot be established, report the evidence and ask before running a build or export that
would write generated output for it.

## Compile-Only Validation

For a production mod project, use a nonexistent `ModPath` so a post-build package copy cannot turn successful C#
compilation into a misleading local-output failure:

```powershell
dotnet build <ProjectPath>.csproj /p:ModPath=__NoSuchModPath__
```

This does not prepare a package for real-game testing. Report that the command was compile-only when the ordinary build
has repository-specific side effects.

Do not parallelize builds of the same project and configuration unless they use isolated intermediate output paths.
Shared `obj` output can produce file-lock failures. Run compile-only and real-package builds sequentially.

## Real-Game Package Output

For code-only changes, resolve the current `_MODS!` alias and pass its absolute target as `ModPath` unless the project
has already been verified to use that destination. Do not rely on a historical machine-specific project default.

For Unity-owned data, use Unity Operational Knowledge and the repository export wrapper. For split ownership such as
SmartHaulers, inspect every changed path and run each owning build or export pipeline.

Verify the expected DLL, XML, manifest, data, bundle, or timestamp after refreshing the package. A compile-only build is
not sufficient before asking the user to validate gameplay, runtime, or UI behavior in the real game.

## Focused Tests

Follow the root real-game gate before creating, modifying, or running regression tests for gameplay, runtime, or UI
behavior. After confirmation:

- run the changed package's focused runner when the matrix lists one;
- for test-only changes, run the changed test project;
- for shared behavior, run or build only downstream packages that actually include the changed behavior;
- require every relevant build and focused runner to pass before submission.

Do not use unrelated mod tests as automatic gates. Tests do not authorize production changes that the task did not
request. If a test exposes a production bug, dead code, missing API, or design mismatch outside the approved fix, stop
and ask.

Configurator-only classes that only bind services or declare contexts do not need unit tests unless they contain
non-trivial logic.

If the user says not to run tests, still run the relevant compile or build check unless the user also says not to build.

## Test Seams And Game Assemblies

Prefer the smallest useful test seam for code that depends on Timberborn or game services. Narrow stubs may remove the
game runtime from a focused test, but model only the behavior required by that test.

Stub-based unit tests do not replace building the changed mod project. Game extension methods, publicized APIs,
namespace imports, assembly shape, and lifecycle behavior may differ from the stub model.

If a test requires large stubs, private-construction reflection, or duplicated lifecycle behavior, first try a smaller
test-only seam. If that remains brittle or obscures the behavior, ask before introducing a production-code testability
change.

## Known Downstream Relationships

`AutomationForModdableWeather` has a project reference to `Automation`. When an Automation change affects the extension
contract used by that plugin, also build `AutomationForModdableWeather` as downstream compile coverage.

TimberDev files are linked directly into multiple production and test projects. Run `TimberDev.Tests` for TimberDev
changes, then run or build only consumers that actually include the changed shared file or behavior.

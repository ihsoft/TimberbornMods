# AI Agent Instructions

This repository contains Timberborn mods.

Before making changes, read this file and the instruction files that apply to the current task. Follow them unless the
user explicitly says otherwise.

## How to choose instructions to read

Always start with this root `AGENTS.md`.

Then read only the instruction files that apply to the current task. Do not load every document blindly.

| Condition | Read |
|--------|--------|
| Generating or modifying C# code | `docs/csharp-formatting-rules-for-ai-agents.md` |
| Modifying Timberborn mod code, data, UI, localization, or package files | `docs/timberborn-modding-rules-for-ai-agents.md` and `docs/timberborn-repository-notes.md` |
| Designing a new feature or new mod | `docs/timberborn-modding-howto-for-ai-agents.md` and `docs/timberborn-lessons-learned.md` |
| Investigating architecture or implementation approach | `docs/timberborn-repository-notes.md` and `docs/timberborn-lessons-learned.md` |
| Editing or reviewing GitHub Wiki pages | `docs/timberborn-wiki-editing-rules-for-ai-agents.md` |
| Working with TimberCommons | `docs/TimberCommons-modding-notes-for-ai-agents.md` |
| Bootstrapping a copied or new repository | `docs/timberborn-new-repository-bootstrap-for-ai-agents.md` |
| Preparing, validating, packaging, or publishing a release | `docs/timberborn-release-publishing-rules-for-ai-agents.md` |
| Organizing or updating agent rules | This `AGENTS.md`, relevant files under `docs/`, and any local `AGENTS.md` whose scope is being changed |

## Rule priority

When instructions conflict, follow this priority:

1. Explicit user instruction for the current task.
2. The closest applicable local `AGENTS.md`.
3. This root `AGENTS.md`.
4. Relevant files under `docs/`.
5. Existing code and repository conventions.

## Rule scope and local AGENTS.md files

This root `AGENTS.md` defines repository-wide rules.

Individual mods may also have their own `AGENTS.md` files. This is encouraged when rules apply only to that mod, its
package layout, release process, tests, public API, or known pitfalls.

When working inside a specific mod:

1. Read this root `AGENTS.md`.
2. Check the mod directory and relevant parent directories for additional `AGENTS.md` files.
3. Follow the most specific applicable rules for files in that scope.

Local `AGENTS.md` files may add or narrow rules for their mod. They should not weaken repository-wide safety rules,
generated-reference rules, localization requirements, release stop conditions, or explicit user instructions.

Use a mod-specific `AGENTS.md` for rules that apply only to one mod, such as:

- package data locations,
- mod-specific test commands,
- release quirks,
- public API compatibility notes,
- known pitfalls of that mod,
- mod-specific localization or UI conventions.

Keep repository-wide rules in this root `AGENTS.md` or in files under `docs/`.

## Core principles

- Evidence over assumptions.
- Read existing files before modifying them.
- Never reconstruct existing files from memory.
- Preserve existing content unless explicitly asked to remove it.
- Make the smallest change that satisfies the task.
- Avoid opportunistic refactoring unless explicitly requested.
- Prefer existing Timberborn architecture over custom architecture.
- Prefer extension over replacement.
- Prefer dependency injection over Harmony when possible.
- Use Harmony only when no reasonable extension point exists.

## Research before implementation

When working on a new feature:

1. Find the closest existing game feature.
2. Study the implementation.
3. Understand ownership of the data.
4. Understand dependencies.
5. Understand save/load behavior.
6. Understand UI integration.
7. Identify extension points.
8. Only then begin implementation.

Prefer understanding over patching.

Prefer evidence over assumptions.

Copy architecture, not implementation.

## Repository file changes

When asked to modify a repository file:

1. Read the current file first.
2. Verify that the retrieved content is complete.
3. Preserve unrelated content.
4. Apply only the requested changes.
5. Keep existing formatting and style.
6. Return or commit the complete updated file, depending on the task.

If the file cannot be read completely, stop and report the problem instead of guessing.

## Rules-maintenance tasks

When the user asks to organize, clarify, or update agent rules, change only rule and rule-documentation files such as
`AGENTS.md` and files under `docs/`.

Other repository files may be changing in parallel by other agents. Do not inspect, interpret, fix, format, stage,
revert, or otherwise account for unrelated non-rule changes during a rules-maintenance task.

## Task checklists

### Rules-maintenance task

- Edit only rule files such as `AGENTS.md`, local `AGENTS.md` files, and files under `docs/`.
- Ignore unrelated non-rule changes in the working tree.
- Run `git diff --check` for edited rule files before committing.
- Stage only the rule files changed for this task.

### Test-only task

- Run the changed test project.
- Do not change production code unless the user explicitly asks.
- If tests expose a production issue, stop and ask.

### Unity-resource task

- Remind the user to rebuild or export Unity assets before real-game testing.

## Required tests

Before submitting a change, run the tests relevant to the changed package.

Use `docs/timberborn-repository-notes.md` as the authoritative source for package-specific test selection.

Short version:

For TimberDev-only changes, run:

```powershell
dotnet run --project TimberDev.Tests\TimberDev.Tests.csproj
```

- For mod changes, run that mod's own tests when they exist.
- For shared behavior changes, run affected package tests.
- For test-only changes, run the changed test project.
- Do not use downstream mod tests as automatic gates unless the changed package actually affects them.

These relevant tests MUST pass before submitting the change.

## Local tools and generated references

- `tools/` contains repository scripts and helper commands that should be tracked in Git.
- `.tools/` contains locally installed external tools and must stay ignored.
- `_DecompiledGame/` contains generated decompiled game sources and must stay ignored.
- `_ExtractedGameAssets/` contains generated extracted game modding assets and must stay ignored.
- Do not edit game DLLs, generated decompiled game sources, or generated extracted game assets.
- Use decompiled game sources as a read-only reference for understanding Timberborn architecture.
- Use extracted game assets as a read-only reference for game blueprints, localizations, UI assets, and shaders.

## Unity resources

When changing Unity project resources (`UXML`, `USS`, localization files, images, sprites, prefabs, or asset bundle
content), remind the user to rebuild the Unity project before testing in the real game.

## Final version requests

When the user asks for a "final version" of any repository file:

1. Read the current file from the repository first.
2. Never reconstruct the file from memory or previous chat context.
3. Preserve existing content unless explicitly asked to remove it.
4. Return the complete updated file.
5. If the current file cannot be retrieved completely, say so.

## Creating new Timberborn mods

When designing or implementing a new Timberborn mod:

1. Find the closest existing game feature.
2. Study the game data and classes behind it.
3. Understand ownership, dependencies, UI integration, and save/load behavior.
4. Prefer existing extension points.
5. Avoid Harmony unless necessary.
6. Add localization for all user-facing text.
7. Consider performance frequency: startup, tick, frame, building, district, or UI open.

Follow `docs/timberborn-modding-howto-for-ai-agents.md` for the full workflow.

## Coding style

Follow `docs/csharp-formatting-rules-for-ai-agents.md`.

Important reminders:

- 2-space indentation for executable code.
- Java/K&R-style braces.
- 120-character line limit.
- `var` when the type is obvious.
- 4-space indentation for wrapped arguments, initializers, expression continuations, and ternary operators.
- Do not split generic method signatures unless absolutely necessary.

## Localization

User-facing text must be localized.

Follow the localization rules in `docs/timberborn-modding-rules-for-ai-agents.md`.

Do not hardcode visible English strings in UI or gameplay messages.

## Publicizer and private access

Before using reflection or `AccessTools`, check whether the relevant game assembly is publicized.

If direct access is available, prefer direct access.

Use reflection only when necessary.

## Safety rule

If unsure, ask or state the uncertainty.

Do not invent Timberborn APIs, services, classes, paths, files, behaviors, or extension points.

## Stop and ask when

Stop and ask the user instead of guessing when:

- the requested file cannot be read completely,
- project intent or architecture is unclear,
- multiple reasonable implementation paths exist,
- a test reveals a production bug, dead code, missing API, or design mismatch but the user did not ask to fix
  production code,
- a release version, source path, package contents, platform ID, or credentials are inconsistent,
- Steam/Mod.IO descriptions differ from local `Workshop` descriptions,
- bootstrap paths such as `_GAME!`, `_MODS!`, `_WORKSHOP!`, or `_LOGS!` cannot be discovered safely,
- a rule change would weaken an existing safety rule.

## Testing rule

Tests must not drive unapproved production-code changes.

If adding or expanding tests reveals a production bug, dead code, missing API, or design mismatch, stop and ask before
changing production code unless the user explicitly asked to fix it.

Configurator-only classes that only bind services or declare contexts do not need unit tests unless they contain
non-trivial logic.

## First task

Before making any code changes:

1. Read AGENTS.md.
2. Read all relevant files from the docs directory.
3. Summarize:
   - repository architecture,
   - coding style,
   - Timberborn modding approach,
   - important repository-specific notes,
   - lessons learned.
4. Confirm understanding before proposing changes.

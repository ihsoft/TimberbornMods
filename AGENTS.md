# AI Agent Instructions

This repository contains Timberborn mods.

Before making changes, read the relevant instruction files listed below and follow them unless the user explicitly says otherwise.

## Required reading

Always read first:

1. `docs/csharp-formatting-rules-for-ai-agents.md`
2. `docs/timberborn-modding-rules-for-ai-agents.md`
3. `docs/timberborn-modding-howto-for-ai-agents.md`
4. `docs/timberborn-repository-notes.md`
5. `docs/timberborn-lessons-learned.md`

When working with TimberCommons, also read:

6. `docs/TimberCommons-modding-notes-for-ai-agents.md`

When bootstrapping a new repository that only copied `AGENTS.md` and `docs/`, also read:

7. `docs/timberborn-new-repository-bootstrap-for-ai-agents.md`

When preparing, validating, or publishing releases to Steam Workshop or Mod.IO, also read:

8. `docs/timberborn-release-publishing-rules-for-ai-agents.md`

## Instruction hierarchy

The instruction files serve different purposes:

| File | Purpose |
|--------|--------|
| `docs/csharp-formatting-rules-for-ai-agents.md` | C# formatting and code style |
| `docs/timberborn-modding-rules-for-ai-agents.md` | Repository-specific Timberborn modding rules |
| `docs/timberborn-modding-howto-for-ai-agents.md` | How to discover, design, and implement new Timberborn mods |
| `docs/timberborn-repository-notes.md` | Repository-specific architectural and workflow notes |
| `docs/timberborn-lessons-learned.md` | Practical discoveries and development experience |
| `docs/TimberCommons-modding-notes-for-ai-agents.md` | TimberCommons-specific implementation notes |
| `docs/timberborn-new-repository-bootstrap-for-ai-agents.md` | New repository setup and first-mod bootstrap workflow |
| `docs/timberborn-release-publishing-rules-for-ai-agents.md` | Release validation and publishing rules |

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

## Read only what is relevant

Do not load all instruction files blindly.

Read:

- formatting rules when generating C# code,
- Timberborn modding rules when modifying repository code,
- Timberborn modding how-to when designing new features,
- repository notes when making architectural decisions,
- lessons learned when investigating implementation approaches,
- TimberCommons notes only when working with TimberCommons.
- new repository bootstrap notes only when setting up a repository that does not yet have local links, generated
  references, or established mod project structure.
- release publishing rules only when preparing, validating, or publishing releases to Steam Workshop or Mod.IO.

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

## Required tests

Before submitting a change, run the tests relevant to the changed package.

For TimberDev-only changes, run:

```powershell
dotnet run --project TimberDev.Tests\TimberDev.Tests.csproj
```

TimberDev is a standalone package that depends only on itself and the game APIs. Do not use other mods as a
TimberDev validation gate by default.

If a TimberDev change affects logic that another mod actually uses, also run that mod's tests as downstream regression
coverage.

For changes in a mod, run that mod's own tests when they exist. If the change touches shared behavior used by multiple
packages, run the affected package tests as well.

For test-only changes, run the test project that was changed. Do not run downstream mod tests for test-only changes
unless the change also modifies shared production code or shared test infrastructure used by those downstream tests.

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

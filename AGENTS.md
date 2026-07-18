# AI Agent Instructions

This repository contains Timberborn mods.

These rules are repository rules for any AI agent working here. Some coordination steps mention Codex thread tools; if
another agent environment does not provide those tools, follow the same role boundaries manually and ask the user to
forward messages or provide the target role contact.

Before making changes, read this file and the instruction files that apply to the current task. Follow them unless the
user explicitly says otherwise.

## How to choose instructions to read

Always start with this root `AGENTS.md`.

Then load the active baseline layers in this order:

1. If `.agents/enable-ada` exists, read `docs/agent-profiles/Ada-Personality-v2.md` for optional communication style
   and personality. If the marker is absent, do not load that profile. Do not infer opt-in from the user's language.
2. Always read `docs/agent-profiles/Engineering-Professional-v3.md` for general engineering reasoning.
3. Always read `docs/agent-knowledge/Timberborn-Domain-Knowledge-v1.md` for Timberborn-specific domain knowledge.

These layers complement this `AGENTS.md`; they do not replace repository, role, safety, local, or task-specific rules.
They remain subject to the rule priority below.

Files named `Operational Knowledge` provide task-specific methods, evidence maps, and decision aids. They are not
baseline profiles or independent safety policy. Load them only when a routing-table condition applies, together with
the relevant repository, role, and local rules.

After the baseline layers, read only the instruction files that apply to the current task. Do not load every document
blindly.

| Condition | Read |
|--------|--------|
| Generating or modifying C# code | `docs/csharp-formatting-rules-for-ai-agents.md` |
| Modifying Timberborn mod implementation: C# behavior, gameplay data, UI structure or behavior, or package metadata | `docs/timberborn-modding-rules-for-ai-agents.md` and `docs/agent-knowledge/Timberborn-Repository-Validation-Operational-Knowledge-v1.md` |
| Adding or modifying player-facing text, localization keys or files, localized UI strings, or `ILoc` usage | `docs/agent-knowledge/Timberborn-Localization-Operational-Knowledge-v1.md` and `docs/agent-knowledge/Timberborn-Repository-Validation-Operational-Knowledge-v1.md` |
| Working with `ModsUnityProject`, Unity-owned package data, compatibility lanes, or Unity export | `docs/agent-knowledge/Timberborn-Unity-Operational-Knowledge-v1.md` |
| Changing Timberborn game, Unity Editor, or Unity package versions; running the importer; refreshing or validating imported game assemblies | `docs/agent-knowledge/Timberborn-Unity-Import-Operational-Knowledge-v1.md` and `docs/agent-knowledge/Timberborn-Unity-Operational-Knowledge-v1.md` |
| Starting any agent-controlled Unity Editor or batch-mode launch for this repository's work, including a separate project, or staging, creating, or verifying a Git commit | `docs/agent-knowledge/Repository-Coordination-Operational-Knowledge-v2.md` |
| Creating or modifying in-game UI Toolkit views, UXML, USS, dialogs, panels, or fragments | `docs/timberborn-ui-toolkit-notes-for-ai-agents.md` |
| Designing a new feature or new mod | `docs/timberborn-modding-howto-for-ai-agents.md`, `docs/timberborn-repository-notes.md`, and `docs/timberborn-lessons-learned.md` |
| Investigating architecture or implementation approach | `docs/timberborn-repository-notes.md` and `docs/timberborn-lessons-learned.md` |
| Diagnosing build, export, mod loading, runtime, UI, save/load, or platform behavior | `docs/agent-knowledge/Timberborn-Diagnostics-Operational-Knowledge-v1.md` |
| Editing or reviewing GitHub Wiki pages | `docs/agent-knowledge/Wiki-Operational-Knowledge-v1.md` and `docs/timberborn-wiki-editing-rules-for-ai-agents.md` |
| Working with TimberCommons | `docs/TimberCommons-modding-notes-for-ai-agents.md` |
| Bootstrapping a copied or new repository | `docs/timberborn-new-repository-bootstrap-for-ai-agents.md` |
| Preparing, validating, packaging, or publishing a release | `docs/timberborn-release-publishing-rules-for-ai-agents.md` |
| Organizing or updating agent rules | This `AGENTS.md`, `docs/agent-knowledge/Mentor-Rule-Maintenance-Operational-Knowledge-v1.md`, relevant files under `docs/`, and any local `AGENTS.md` whose scope is being changed |

## Rule priority

When instructions conflict, follow this priority:

1. Explicit user instruction for the current task.
2. The closest applicable local `AGENTS.md`.
3. This root `AGENTS.md`.
4. Relevant files under `docs/`.
5. Existing code and repository conventions.

## Agent roles

Every new agent is a coder by default unless the user explicitly assigns another role for the current thread.

Roles are intentionally separate. Do not broaden your own role just because you notice adjacent work.

### Coder

The coder implements ordinary repository work: code, tests, mod data, localization, package changelog entries, and
task-specific documentation that belongs with the implementation.

The coder does not own agent rules, GitHub Wiki editing, or publishing mods to Steam Workshop or Mod.IO.

If a coder discovers a useful rule, workflow, release, or Wiki lesson while implementing code, the coder should send a
delegated suggestion to the appropriate role instead of changing that role's files directly.

If the user asks a coder to add or update an agent rule, treat that as a mentor task unless the user explicitly expands
the current role to mentor or rules-maintenance work. Do not self-switch roles.

### Mentor

The mentor owns agent rules and rule organization for this repository.

The mentor may edit this `AGENTS.md`, local `AGENTS.md` files, files under `docs/`, and other rule or role
documentation. The mentor decides whether delegated suggestions should be accepted, narrowed, rewritten, moved, or
rejected.

The mentor does not need to preserve another agent's proposed wording or target file. The mentor reports which files
actually changed.

The mentor optimizes for improving the quality of future engineering decisions, not for proving that the mentor is
right. Good mentor outcomes include finding conceptual flaws, confirming sound reasoning, exposing unsupported
assumptions, and helping a good idea become clearer.

When evaluating delegated suggestions, the mentor should judge the underlying problem model before editing wording.
Watch for hypotheses presented as settled facts, especially in research-heavy or prototype docs. If reasoning is
unclear, say that it is unclear instead of treating it as wrong. Prefer honest uncertainty over confident explanations
that are not supported by evidence. Before rejecting a suggestion, consider why a capable agent may have proposed it
and whether the wording encodes a hidden constraint. Mentor feedback should optimize for decision quality, not for the
number of comments.

### Publisher

The publisher owns release preparation, validation, packaging, publishing to Steam Workshop or Mod.IO, platform
description update proposals when release scope changes the mod's player-facing capabilities, and post-release issue
closing workflow.

The publisher follows `docs/timberborn-release-publishing-rules-for-ai-agents.md`. The publisher should not implement
unrelated code fixes, edit agent rules, or edit the GitHub Wiki unless the user explicitly expands the task.

### Wiki editor

The Wiki editor owns the separate GitHub Wiki checkout. The expected local layout is a sibling repository named
`<repo-root>.wiki`, not a folder inside the main repository.

The Wiki editor follows `docs/agent-knowledge/Wiki-Operational-Knowledge-v1.md` and
`docs/timberborn-wiki-editing-rules-for-ai-agents.md`. The Wiki editor should not implement code changes, publish mods,
or edit agent rules unless the user explicitly expands the task.

## Delegating role-specific suggestions

When a coder has a rule suggestion, send it to the mentor instead of editing rule files.

When the user asks a coder to suggest, collect, or summarize possible rule changes, treat that as a request to delegate
the suggestions to the mentor unless the user explicitly asks to only show the ideas in the current thread.

In Codex or another environment with thread tools, search for the mentor thread with `list_threads`. Start with or fall
back to the role name alone, such as `Mentor`, then verify the returned thread's title and current repository before
sending. More specific queries such as `TimberbornMods mentor` or `mentor rules` are useful, but an empty result for a
specific query is not evidence that no mentor thread exists. If exactly one matching mentor thread is found, send a
`codex_delegation` message to it with `send_message_to_thread`.

If thread tools are unavailable, or if no clear mentor thread is found, do not create a new thread and do not edit the
rules yourself. Tell the user that you could not reach the mentor and ask for the mentor thread ID, contact, or for the
user to forward the suggestion.

Use this format for mentor suggestions:

```xml
<codex_delegation>
  <source_thread_id>THREAD_ID_IF_KNOWN</source_thread_id>
  <input>
Observation:

Evidence:

Suggested rule scope:

Suggested wording:

Risk:
  </input>
</codex_delegation>
```

For publishing suggestions, delegate to the publisher thread when the user has provided one or when a clear `Publisher`
thread can be found. For Wiki suggestions, delegate to a clear `Wiki editor` thread the same way.

## Answering delegated help requests

When another agent asks your role for help, diagnostics, or a decision, treat the source thread as an open requester.
You may investigate in your own thread and ask the user for missing context when needed, but once you have a conclusion,
partial finding, committed fix, or blocker, send a concise answer back to the requester.

The answer should include the conclusion, the main evidence, and the next action or blocker. Do not leave the requesting
agent waiting just because the fix or investigation was completed elsewhere.

## Role learning handoff

At the end of a non-trivial task, each role agent should briefly check whether it learned something durable that would
help future agents.

Treat task signoff, a final "done" report, a post-release conclusion, or "no more diagnosis needed" as the end of the
task for this check. Do not wait for the user to ask for a mentor handoff. If the task produced a durable lesson that
meets the criteria below, send it to the mentor as part of closing the task.

Send a suggestion to the mentor only when the observation is:

- likely to repeat in future work,
- specific enough to become a rule, guardrail, workflow note, or local `AGENTS.md` note,
- backed by concrete evidence from the task,
- not already covered clearly by existing rules.

Good mentor-note candidates include:

- the user corrected a workflow, safety, role, release, Wiki, test, or build assumption,
- an existing rule was missed because it was hidden, vague, too broad, too narrow, or not in the files the role
  naturally read,
- a repeated repository, mod, platform, localization, build, or test pitfall became clear,
- a workflow step behaved differently from what the rules implied,
- a local mod needs a new or updated `AGENTS.md` note because the lesson is mod-specific.

Do not treat "I forgot" as an acceptable explanation. Agents are expected to follow applicable rules.

If a rule was missed, the mentor note must analyze the process failure instead of excusing it:

- which applicable rule or expectation was missed,
- which files or instructions the agent did or did not read,
- what task step should have triggered the rule,
- why the current checklist or workflow failed to catch it,
- what concrete rule placement, checklist item, or role-specific reminder might prevent recurrence.

Do not send mentor notes for raw logs, long transcripts, speculative ideas without evidence, or suggestions that only
say "be careful". The goal is not blame. The goal is to make future rule-following harder to miss.

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

Keep this root `AGENTS.md` as the routing layer. Put detailed, role-specific, workflow-specific, or mod-specific rules
in the closest relevant linked file. Avoid duplicating the same rule in several places; use pointers unless a local
reminder is needed to prevent repeated misses.

## Core principles

- Evidence over assumptions.
- Read existing files before modifying them.
- Never reconstruct existing files from memory.
- Preserve existing content unless explicitly asked to remove it.
- Make the smallest change that satisfies the task.
- Avoid opportunistic refactoring unless explicitly requested.
- Do not invent missing user intent. If a request has multiple plausible meanings, clarify before acting, especially
  before public, external, destructive, historical, release, tag, Wiki, or platform-facing changes.
- Treat player-facing and user-facing text as high-impact content. Do not silently shorten, rewrite, or add visible
  text. Preserve useful meaning, avoid adding noise, and ask for review when a technical limit or workflow requires a
  meaningful text change.
- Prefer existing Timberborn architecture over custom architecture.
- Prefer extension over replacement.
- Prefer dependency injection over Harmony when possible.
- Use Harmony only when no reasonable extension point exists.
- Localize player-facing text; do not hardcode visible English strings in UI or gameplay messages.
- Before using reflection or `AccessTools`, check for direct access through publicized game assemblies.

## Portable local paths

Use portable path conventions in repository rules and documentation:

- `<repo-root>` means the current main repository root.
- `<repo-root>.wiki` means the expected sibling GitHub Wiki checkout.
- `_GAME!`, `_MODS!`, `_WORKSHOP!`, and `_LOGS!` are local environment aliases discovered from the current
  checkout/config/tools, not portable absolute paths.

Do not encode machine-specific absolute paths in repository rules.

If an expected local path, alias, checkout, generated reference folder, or tool is missing, stop and ask the user. Do
not merely report that it is missing. Explain what the missing item is used for and propose the next setup action, such
as locating an existing path, creating a local link, cloning the Wiki checkout, or continuing without the optional
resource when the current task does not need it.

The GitHub Wiki is a separate Git repository. The expected URL is:

```text
https://github.com/ihsoft/TimberbornMods.wiki.git
```

The recommended local checkout is the sibling path `<repo-root>.wiki`. If it is missing and Wiki work is needed, ask
whether to clone it there, locate an existing checkout, or continue without Wiki edits. Do not create Wiki pages inside
the main repository.

## Research before implementation

For a new feature, find the closest existing game feature and investigate the applicable data ownership, dependencies,
persistence, UI integration, and extension points before implementing. Record any item that could not be established
and why it does not block a reversible next step.

Research should answer a concrete implementation decision, not attempt to prove exhaustively that no alternative
exists. Use the bounded research workflow in `docs/timberborn-modding-howto-for-ai-agents.md` when the answer is not
readily available.

## Repository file changes

When asked to modify a repository file:

1. Read the current file first.
2. Verify that the retrieved content is complete.
3. Preserve unrelated content.
4. Apply only the requested changes.
5. Keep existing formatting and style.

If the file cannot be read completely, stop and report the problem instead of guessing.

When the user asks for a "final version," re-read the current repository file instead of reconstructing it from memory
or previous chat context, then return the complete updated content.

## Shared repository coordination

Before any agent-controlled Unity launch for this repository's work, including a separate temporary or cloned project,
or before a Git staging-and-commit transaction, follow
`docs/agent-knowledge/Repository-Coordination-Operational-Knowledge-v2.md` and acquire the resource-specific repository
lock from the main repository. Do not lock ordinary reads, file edits, diagnostics, or independent builds.

The lock serializes an already authorized operation; it does not grant permission to commit, publish, tag, open an
interactive application, or change external state.

## Rules-maintenance tasks

When the user asks to organize, clarify, or update agent rules, change only rule and rule-documentation files such as
`AGENTS.md` and files under `docs/`.

Other repository files may be changing in parallel by other agents. Do not inspect, interpret, fix, format, stage,
revert, or otherwise account for unrelated non-rule changes during a rules-maintenance task.

When a rule change comes from another agent's delegated suggestion and the source thread is known, notify that source
thread after the rule files are finalized. Tell the source agent which files actually changed and that they should
refresh those files or update their checkout. Report the files that were really edited, even if they differ from the
files the source agent expected or requested.

After any committed rule change, notify the dedicated `Publisher` and `Wiki editor` threads when thread tools are
available. These roles must refresh their checkout or context after repository rules change, even when the change did
not start from them.

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

### Mode-shifted task

- If a task starts as investigation, diagnostics, review, or reproduction work and then turns into an implemented fix or
  committed change, re-run the applicable submission checklist before committing.
- For player-visible or user-visible mod behavior changes, this includes re-checking the package changelog rule in
  `docs/timberborn-repository-notes.md`.
- Do not carry forward only the investigation checklist into the final commit.

### Real-game validation gate

- For gameplay, runtime, or UI behavior changes in any mod, user real-game validation is the default submission gate
  before tests or commit.
- This is an implementation-submission gate, not a requirement for the Publisher to repeat behavior validation during
  routine release preparation. For Publisher work, treat already committed implementation as having passed its
  implementation gates unless the user or release evidence identifies an unresolved runtime problem. Rebuilding,
  exporting, or packaging that committed implementation does not by itself reopen the real-game validation gate.
- The Publisher still owns release-specific build, export, package, identity, platform, and immutable-artifact
  validation, but must not describe those checks as player-behavior testing. If release work exposes a credible
  unresolved runtime problem, stop publication and report it for implementation ownership or explicit user direction;
  do not silently assume either that the release is safe or that the Publisher has behavior-tested it.
- Real-game validation establishes that the production change actually satisfies the requested behavior. A passing test
  written against an unverified approach can preserve the wrong behavior while appearing correct.
- Build and export the changed mod into the real local package output first, using the appropriate C# build and Unity
  export paths for the changed files.
- Then tell the user the change is ready to test in game and wait for the user's validation result. The user performs
  the in-game test, not the agent.
- Pause creating, modifying, and running tests, as well as commit, until the user confirms the in-game behavior, unless
  the user explicitly accepts testing or committing before real-game validation.
- After confirmation, add or update focused tests for the behavior that was actually validated and run the relevant
  test projects. These tests are regression protection for the confirmed behavior, not a substitute for real-game
  validation.
- A generic "commit" instruction does not automatically remove this gate. Before committing gameplay, runtime, or UI
  behavior changes, either report the user's in-game validation result or state that the user explicitly accepted
  committing without it.

### Unity-resource task

- Use Timberborn Unity Operational Knowledge and the package matrix to select the owner, compatibility lane, and export
  path.
- When Unity-owned package data changed, run repository export tooling before real-game validation. Never hand-copy it
  into `_MODS!`; if export was not run, report that the local package was not refreshed.

## Required build and test validation

After any applicable real-game validation gate, use the `Package Build And Validation Matrix` in
`docs/agent-knowledge/Timberborn-Repository-Validation-Operational-Knowledge-v1.md` to select package-specific compile,
export, and focused test commands.

- For test-only changes, run the changed test project.
- For shared behavior changes, run or build only the downstream packages that actually include the changed behavior.
- All relevant builds and focused tests must pass before submitting the change.

## Local tools and generated references

- `tools/` contains repository scripts and helper commands that should be tracked in Git.
- `.tools/` contains locally installed external tools and must stay ignored.
- `_DecompiledGame/` contains generated decompiled game sources and must stay ignored.
- `_ExtractedGameAssets/` contains generated extracted game modding assets and must stay ignored.
- Do not edit game DLLs, generated decompiled game sources, or generated extracted game assets.
- Use decompiled game sources as a read-only reference for understanding Timberborn architecture.
- Use extracted game assets as a read-only reference for game blueprints, localizations, UI assets, and shaders.

## New mod repository setup

Follow `docs/timberborn-modding-howto-for-ai-agents.md` for the implementation workflow. For repository integration:

1. Define C# and package-data ownership, compile-only validation, real-game build/export, and focused test selection;
   add or update the mod's row in the `Package Build And Validation Matrix` in
   `docs/agent-knowledge/Timberborn-Repository-Validation-Operational-Knowledge-v1.md`.
2. Decide whether durable mod-specific rules justify a local `AGENTS.md`. Do not create an empty local instruction file
   only to satisfy the checklist.

## Safety rule

If unsure, ask or state the uncertainty.

Do not invent Timberborn APIs, services, classes, paths, files, behaviors, or extension points.

## Stop and ask when

Stop and ask the user instead of guessing when:

- the requested file cannot be read completely,
- project intent or architecture is unclear,
- the user's request can reasonably mean more than one workflow or scope, especially if one interpretation would publish,
  tag, backfill, delete, rewrite history, edit Wiki pages, change platform metadata, or otherwise affect public state,
- several reasonable implementation paths remain and the choice would materially affect task scope, public API,
  compatibility, player-visible behavior, persisted data, external state, or the cost of reversing the change,
- a test reveals a production bug, dead code, missing API, or design mismatch but the user did not ask to fix
  production code,
- a release version, source path, package contents, platform ID, or credentials are inconsistent,
- Steam/Mod.IO descriptions differ from local `Workshop` descriptions,
- bootstrap paths such as `_GAME!`, `_MODS!`, `_WORKSHOP!`, or `_LOGS!` cannot be discovered safely,
- a rule change would weaken an existing safety rule.

Do not stop merely because more than one implementation is possible. When the remaining choices are internal,
low-risk, and reversible, choose the smallest evidence-supported approach and briefly state any material tradeoff.

## Repository onboarding

Always load the instructions required by `How to choose instructions to read` before changing files. A routine,
well-scoped task does not require a separate onboarding summary or user confirmation before work begins.

Perform an explicit onboarding review when first working broadly in this repository, designing a new feature or mod,
investigating unfamiliar architecture, or when the user asks for an understanding summary. In that review:

1. Read this `AGENTS.md` and the relevant files under `docs/`.
2. Summarize the task-relevant architecture, coding style, Timberborn approach, repository-specific notes, and lessons
   learned.
3. Identify any material unknowns or assumptions.
4. Ask for confirmation only when an unresolved choice could materially change the intended direction; otherwise
   proceed with the task.

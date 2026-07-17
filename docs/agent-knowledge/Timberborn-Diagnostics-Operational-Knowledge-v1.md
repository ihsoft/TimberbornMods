# Timberborn Diagnostics Operational Knowledge

## Purpose

Provide a bounded, evidence-driven workflow for diagnosing Timberborn mod problems without wandering through every
possible subsystem or applying speculative fixes.

This document controls diagnostic method, not the implementation or release policy for a particular subsystem. Also
read every other instruction selected by the root routing table for the affected package and task. In particular:

- use Timberborn Repository Validation Operational Knowledge for package ownership and build, export, and test
  selection;
- use Timberborn Unity Operational Knowledge for Unity source, export, and package-layer ownership;
- use Timberborn Unity Import Operational Knowledge for importer, game-assembly, Editor-version, and package-dependency
  readiness;
- use the bootstrap rules for missing tools, aliases, generated references, Unity installation, or local environment;
- use the UI Toolkit notes for in-game UI problems;
- use the release rules for package, platform, upload, or post-release problems;
- use the closest local `AGENTS.md` for mod-specific behavior and exceptions.

## Define The Diagnostic Outcome

Start with one observable mismatch:

- expected behavior;
- observed behavior;
- the smallest known reproduction;
- when the behavior last worked, if known;
- the game version and compatibility lane involved.

Do not begin with a presumed cause such as "Harmony is broken" or "Unity did not export." Treat causes as hypotheses
until evidence distinguishes them.

If the user requested diagnosis only, determine and report the cause or narrowest supported boundary. Do not implement a
fix merely because the likely change is visible.

## Establish A Fresh Evidence Baseline

Before interpreting code behavior, verify that the evidence belongs to the attempted run:

1. Identify the source revision and files expected to affect the behavior.
2. Identify the package project, package-data owner, compatibility lane, and build/export path from the package matrix.
3. Verify that the tested local package contains the expected fresh DLL, manifest, data, or asset output.
4. Verify the target game version, enabled mod, required dependencies, and relevant load context.
5. Capture the first relevant error or mismatch from the current attempt, not an older log entry.

A stale package, wrong compatibility lane, disabled mod, missing dependency, or old log is an environment or delivery
problem until proven otherwise. Do not diagnose it as production-code behavior.

## Find The First Failing Boundary

Trace the workflow in execution order and stop at the first boundary that does not produce its expected output:

```text
environment and references
  -> C# compilation
  -> post-build copy or Unity export
  -> local package contents
  -> game discovery and dependency resolution
  -> mod initialization, DI, and patches
  -> runtime behavior or UI
  -> save/load persistence
  -> release artifact and external platform
```

Later symptoms may be consequences of an earlier failure. For example, a missing UI element is not yet a UI layout
problem if the mod never initialized.

## Evidence Map

| Boundary | Start with | Route detailed handling to |
|---|---|---|
| Environment and references | aliases, tool/editor/game versions, generated-reference provenance, license readiness | bootstrap rules |
| C# compilation | the first compiler error and the compile-only command from the package matrix | repository validation knowledge and modding rules |
| Post-build copy | build success versus copy-target failure, resolved `ModPath`, output timestamps | repository validation knowledge |
| Unity import readiness | the first assembly-load or package-dependency error in the current importer or Editor log | Unity Import knowledge, then bootstrap |
| Unity export | the current per-run log under `.tools/unity-logs`, then global Unity `Editor.log` if execution stopped before the batch method | Unity knowledge, then release rules when applicable |
| Local package | manifest, compatibility folder, DLL/data/assets, timestamps, exact source-to-output ownership | Unity and repository validation knowledge, then release rules |
| Game discovery and load | current runtime logs under `_LOGS!`, enabled-mod state, dependencies, first load or initialization exception | modding rules and local `AGENTS.md` |
| Runtime behavior | minimal reproduction, owning game system, lifecycle and timing, narrow repository logs | modding how-to and lessons learned |
| UI behavior | mod initialization first, then template, styles, localization, panel/dialog lifecycle, and visual result | UI Toolkit notes |
| Save/load | fresh versus existing save, save schema or identity, load phase, state before save and after load | modding rules, lessons learned, and local notes |
| Release or platform | verified local artifact, preflight report, platform command response, uploaded artifact comparison | release rules |

Use the nearest evidence source. Do not start with decompiled architecture when the compiler already identifies a syntax
error, or with platform metadata when the local package has not been verified.

## One-Hypothesis Diagnostic Loop

For each iteration:

1. State one falsifiable hypothesis tied to the first failing boundary.
2. Choose the cheapest check that would distinguish it from the main alternative.
3. Run or request that check without bundling unrelated fixes.
4. Compare the result with the prediction.
5. Keep, revise, or reject the hypothesis before taking another step.

Prefer read-only inspection first. If observation is insufficient, use the smallest reversible probe, such as narrow
temporary logging or a controlled reproduction. Do not combine several behavior changes just to see whether the symptom
disappears.

Temporary observability must identify the boundary, relevant identity, and state transition without flooding per-frame
or per-tick logs. Remove it before submission unless the logging has durable operational value and matches repository
logging rules.

## Stop Or Pivot

Stop expanding the search when any of these conditions is met:

- evidence identifies the first failing boundary and the next fix decision;
- the current model explains the observations well enough to make a small reversible next step;
- the relevant independent sources repeat the same absence or uncertainty and another search path is unlikely to
  distinguish the remaining hypotheses;
- the remaining unknown would materially change scope, compatibility, player-visible behavior, persisted data,
  external state, or reversibility and therefore requires a user decision.

Failure to find an API, log entry, precedent, or extension point is not proof that it does not exist. Report it as not
found and list the important sources checked. Do not search indefinitely for an exact vanilla precedent when a bounded,
labeled experiment can answer the actual behavior question.

## From Diagnosis To Fix

Diagnosis does not carry submission approval forward automatically. When the task changes from investigation to an
implemented fix:

1. Re-read the instructions selected by the changed files and package.
2. Re-run the applicable submission checklist.
3. Update the package changelog when the final change is player-visible.
4. Build or export the real local package.
5. Wait for user real-game validation before creating, changing, or running regression tests.
6. After confirmation, add focused regression coverage for the behavior actually validated.

## Diagnostic Report

A useful diagnostic handoff is concise and contains:

- expected and observed behavior;
- environment, version, compatibility lane, and package freshness;
- first failing boundary;
- evidence and sources checked;
- current hypothesis and confidence;
- next discriminating check, proposed fix, or user decision required.

Do not substitute a raw log dump for the conclusion. Include the smallest relevant error excerpt or location needed to
support the finding.

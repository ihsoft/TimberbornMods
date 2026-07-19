# Timberborn New Mod Operational Knowledge

## Purpose

Provide the repository workflow for turning an agreed player-facing idea or working prototype into a maintainable active
Timberborn mod package. The goal is to make known integration decisions cheap without forcing every new-mod task to
load broad architecture notes, historical lessons, or release procedures up front.

This document does not replace:

- the root role, safety, real-game validation, or localization gates;
- Timberborn Modding Rules for implementation details;
- specialized Unity, UI, localization, Timbermesh, runtime-mesh, or diagnostic knowledge when those assets apply;
- the Package Build And Validation Matrix for package-specific commands;
- release publishing rules or Publisher authorization.

## Loading Boundary

Use this document as the default route for creating or integrating a new mod project. Do not automatically load the
entire Modding How-To, Repository Notes, and Lessons Learned collection.

Load those larger references only when the task actually requires architecture investigation, the closest game
precedent is unclear, repository history affects the decision, or the bounded workflow below cannot resolve a material
unknown. Continue to use the root routing table for C#, UI, localization, Unity, mesh, diagnostics, and release work.

## 1. Establish The Mod Boundary

Before scaffolding, state:

- the player problem and the smallest player-visible capability that solves it;
- why this should be a separate mod rather than an addition to an existing package;
- the closest stock feature or repository mod that can provide an architectural precedent;
- material persistence, UI, performance, dependency, and compatibility concerns already known;
- which claims are confirmed, inferred, or still hypotheses.

Do not require exhaustive research. Resolve the decisions that determine package shape or make later reversal costly.
For an internal, low-risk uncertainty, choose the smallest reversible evidence-supported experiment and preserve the
unknown for real-game validation.

### Porting Or Reviving A Legacy Mod

Before copying or modifying a legacy mod, establish its license, the user's authority to port it, and which code or
assets may be redistributed. Keep an external or original upstream checkout read-only unless the user explicitly owns
and authorizes changes there; perform migration work in the repository's intended working project and do not encode the
upstream machine's absolute paths.

Inventory the legacy source before choosing a migration plan. Classify code, data, and assets as:

- active and referenced by the shipped package;
- generated output with an identifiable owner;
- commented out, unused, or historical;
- tied to an obsolete framework or asset pipeline;
- uncertain and requiring evidence or a user decision.

Preserve the confirmed player-facing behavior, not every historical implementation. Do not automatically revive dead
features, commented patches, unused assets, or old dependencies merely because they exist in the source tree. Ask before
expanding the port to intentionally retired or optional behavior.

For each active behavior, find the closest current stock feature and map the old concept to current game architecture.
Treat old APIs, injection styles, specifications, prefabs, and build systems as evidence of past intent, not as the
target design. Separate migration by the asset lanes that actually survive the inventory, and keep unresolved behavior
or ownership choices explicit rather than hiding them inside a mechanical full-project conversion.

## 2. Choose Architecture And Asset Ownership

Trace enough of the closest game feature to decide who owns data, behavior, registration, UI, and persistence. Prefer
existing game extension points and dependency injection; do not begin with Harmony or custom replacement systems.

Choose only the asset lanes the feature needs:

- a C# project for new runtime behavior;
- loose package data for supported blueprints, manifests, localizations, and other directly loaded resources;
- `ModsUnityProject` for Unity-owned assets and bundles;
- reproducible raw Timbermesh generation when the model belongs to that format;
- UI Toolkit assets only when the feature owns or extends player-facing UI.

Split ownership explicitly when a mod uses more than one lane. Do not create a Unity project, test project, custom
generator, compatibility lane, or Harmony patch merely because another mod has one.

If the architecture or ownership choice remains material and unclear, load the Modding How-To plus the relevant
Repository Notes and Lessons Learned, record the evidence checked, and ask only when the remaining choice affects
scope, public API, persisted data, compatibility, player-visible behavior, external state, or reversibility.

## 3. Define Stable Local Identity

Choose one coherent mod-level identity for the repository directory, project and assembly where applicable, package
manifest, namespace, package output, and changelog. Building, template, resource, localization, and faction-specific
identities may remain narrower than the mod identity; do not mechanically rename valid child identities.

Define:

- package-data owner and real local package destination;
- initial game compatibility lane or lanes supported by evidence;
- required mod or assembly dependencies;
- DLL and manifest ownership;
- localization ownership for every player-facing string;
- generated-output owners and reproducible commands, if any.

Treat compatibility folders as actual compatibility lanes, not one folder per game version. Keep credentials and local
machine paths out of tracked files. Do not invent platform IDs or add release-only configuration such as `release.json`
unless the user has moved the task into release preparation.

## 4. Create The Smallest Repository Slice

Create only the projects and package files required by the chosen ownership model. Follow existing repository and
nearby active-mod conventions, but do not copy a complete project layout blindly.

For an active mod, ensure the applicable slice includes:

- the production project when code is required;
- a valid package manifest and required package resources;
- localized player-facing text through the repository localization workflow;
- a package changelog with an honest `(TBD)` section for player-visible work;
- manifest dependencies and implementation-owned compatibility data, while deferring release-only platform config;
- a local `AGENTS.md` only when durable mod-specific contracts justify one.

Do not create empty tests or local instructions merely to satisfy a checklist. A missing focused test project must be
recorded honestly rather than replaced with unrelated tests.

## 5. Register Build And Validation Ownership

Adding a new active mod is not complete until the Package Build And Validation Matrix records its production project,
package-data owner, focused-test status, and extra local instructions. Update that row when any of those owners change.

Establish and report separately:

- compile-only validation;
- the ordinary build or export that refreshes the real `_MODS!` package;
- the exact package files and compatibility lane expected after that operation;
- focused regression tests that apply after real-game confirmation.

Build or export the real package before asking for player validation. Follow the root gate: the user confirms gameplay,
runtime, or UI behavior in the game first; focused tests then protect the confirmed behavior. Do not claim save/load,
compatibility, logistics, performance, or faction behavior that was not exercised.

## 6. Hand Off Release Work

Ordinary new-mod implementation does not create Steam Workshop items, Mod.IO pages, platform IDs, Git tags, GitHub
Releases, or public visibility. When the user requests first publication, hand the committed implementation and its
evidence to the Publisher and load the release publishing rules.

The handoff should identify the mod identity, intended first version, current compatibility lanes, package source,
validated build/export path, changelog state, dependencies, descriptions or thumbnail already prepared, known missing
platform identities, and any behavior that remains unconfirmed. Platform identity bootstrap and publication remain
separately authorized Publisher work.

## Completion Check

A new mod is repository-ready when:

1. its player goal, architecture, identity, dependencies, and asset owners are explicit;
2. the smallest required source and package slice exists;
3. player-facing text and changelog ownership are established;
4. the validation matrix and any justified local instructions are current;
5. the real package has been built or exported through its owning paths;
6. behavior evidence and remaining unknowns are reported honestly;
7. tests, when useful, follow real-game confirmation rather than substitute for it;
8. release preparation remains a separate explicit handoff.

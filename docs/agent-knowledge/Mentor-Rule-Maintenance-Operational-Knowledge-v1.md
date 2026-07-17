# Mentor Rule Maintenance Operational Knowledge

## Purpose

Help the mentor classify, route, version, split, merge, and retire repository agent instructions without turning the
root `AGENTS.md` into an encyclopedia or making ordinary task routes load irrelevant detail.

This document is for rules-maintenance work. It does not replace the root role, safety, delegation, file-editing,
validation, commit-isolation, or role-notification rules.

## Choose The Instruction Layer

Place a rule according to who needs it and which task should load it:

| Layer | Owns |
|---|---|
| Personality profile | optional communication style, tone, and character |
| Professional profile | general reasoning and professional methodology |
| Domain Knowledge | compact, stable conceptual understanding needed across the domain |
| Operational Knowledge | task-specific workflow, gates, evidence maps, and decision aids |
| Root `AGENTS.md` | repository-wide roles, safety, priority, routing, and short universal gates |
| Local `AGENTS.md` | rules for one mod, package, directory, public API, or local workflow |
| Specialized workflow document | bootstrap, release, Wiki, or another large role-specific process |

Ask: **Who needs this rule, and during which task should it enter context?** Put it in the narrowest layer that reliably
answers that question. Do not promote repository-specific procedures into baseline domain knowledge merely because they
are important.

## Routing And Context Cost

A document without a discoverable routing condition is effectively orphaned. When creating or moving instructions,
add or update the root route that makes the intended task load them.

Optimize loaded task routes, not total bytes under `docs/`:

- keep the root as a routing layer rather than a detailed encyclopedia;
- move rare, coherent workflows out of files loaded by common tasks;
- accept a larger total corpus when specialization makes frequent routes materially smaller or clearer;
- avoid splitting a small rule when the new file would always be loaded with its source and would add navigation without
  changing a decision;
- after a routing change, compare representative common and specialized task routes before declaring the split useful.

## Authoritative Ownership And Duplication

Give each rule one authoritative home. Other documents should point to it or keep only the shortest reminder needed to
trigger the correct route.

Duplicate a rule only when concrete evidence shows that agents repeatedly miss the authoritative source and a local
reminder is the smallest effective fix. Do not duplicate merely because a rule is important.

After moving a rule, search for old wording, stale paths, and conflicting summaries. A more detailed copy is not
automatically more authoritative; ownership comes from the selected layer and routing model.

## Document Lifecycle

Create a new document when at least one of these is true:

- the content forms a coherent workflow with its own routing condition;
- an existing document mixes materially different ownership or task phases;
- a rare section significantly burdens a frequent route;
- the new boundary makes a safety gate or decision point easier to discover.

Do not create a new document for one small rule that fits an existing authoritative layer, for content with no distinct
route, or when the split adds more navigation than clarity or context savings.

Consider merging or retiring documents when they are always loaded together, their boundary no longer affects agent
decisions, one fully replaces another, or a file has become historical and unreferenced. Keep exactly one active source
for each current instruction contract.

## Versioning As Contract Generations

Treat a `vN` filename suffix as a contract generation, not as a document revision counter. Git history already records
additions, corrections, and incremental refinement.

Do not bump the version for:

- new evidence, examples, supported cases, or checklist items within the existing purpose;
- clearer wording or correction of an error that does not change the document's behavioral contract;
- internal reorganization that preserves ownership and required agent decisions.

Bump the version when:

- the document's purpose or ownership changes materially;
- the old and new rules direct a competent agent to take materially different actions for the same input;
- a core default, mandatory gate, or required workflow order changes incompatibly;
- a split or merge removes a substantial responsibility from the existing document;
- consumers must update references because the previous contract is no longer interchangeable with the new one.

Use this control question:

> Would an agent following the old document make a materially different decision from an agent following the new one?

If yes, a new generation is probably warranted. A large rewrite that preserves the contract does not require a bump,
while a small reversal of a critical default may require one.

Layer-specific examples:

- bump a personality profile when its communication contract changes materially;
- bump a professional profile when its reasoning methodology changes incompatibly;
- bump Domain Knowledge when the conceptual model changes, not when another fact is added;
- bump Operational Knowledge when workflow ownership, defaults, or mandatory gates change incompatibly.

Do not introduce minor or patch suffixes for living repository instructions. Keep only one active version after the
migration unless the user explicitly approves a temporary coexistence period.

## Structural Change Checklist

After a document split, merge, rename, retirement, or version bump:

1. Recheck the purpose and ownership of every resulting file.
2. Update every routing condition and direct role pointer that should load it.
3. Search for the old filename, stale wording, and obsolete version references.
4. Remove the previous active version after the migration; do not leave two implicit sources of truth.
5. Verify that no mandatory rule became unreachable from its task route.
6. Compare the context cost of representative common and specialized routes.
7. Run the root rules-maintenance validation and inspect the complete rule-only diff.
8. Prefer an isolated structural commit so later content changes remain reviewable.

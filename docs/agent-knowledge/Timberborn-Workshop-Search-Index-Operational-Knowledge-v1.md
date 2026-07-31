# Timberborn Workshop Search Index Operational Knowledge

## Purpose

Use this knowledge when changing the public Timberborn Workshop search-index pipeline, its collection and classifier
tools, its GitHub Actions workflows, its published data contract, or a frontend or in-game consumer of that data.

This document owns decision boundaries, safety controls, compatibility expectations, and validation strategy. Tool
commands and exact record fields belong in the closest tool README and the public data-contract document.

## System Boundary

The production pipeline has four distinct stages:

1. Build a complete anonymous HTTP snapshot of all publicly discoverable Timberborn Workshop item kinds.
2. Resume and refresh public gallery URLs only for records classified as maps, using an anonymous Steam game-server UGC
   session.
3. Reuse or calculate per-image visual scores, then recompute map aggregates and corpus-relative percentiles.
4. Merge and publish compact versioned artifacts through GitHub Pages.

Preserve these scope boundaries:

- metadata covers all discovered item kinds;
- gallery collection and visual classification cover maps only;
- non-map search uses metadata, tags, and coarse category evidence, not visual features;
- public image URLs are evidence inputs, while Workshop package contents are outside this pipeline;
- downloaded images are transient classifier inputs and are not part of the published corpus.

Titles, descriptions, tags, categories, visual labels, and similarity scores are evidence for discovery. They are not
ground truth for map terrain, geometry, compatibility, or item behavior.

## Anonymous Execution Contract

The recurring pipeline must remain runnable on a clean GitHub-hosted runner without a Steam account, Steam login, API
key, repository secret, local Steam client, or Timberborn process.

The gallery indexer is an intentional anonymous `SteamGameServerUGC` path. Do not replace it with authenticated
Steam-client access merely to obtain more data or reuse a familiar repository CLI pattern. Any proposal to introduce
account-bound access, credentials, package downloads, or a game launch is an architecture and security change that
requires explicit user approval after the security and operations trade-offs are documented.

Read-only access is part of the contract. Neither metadata nor gallery collection may subscribe to items, mutate
Workshop state, or download Workshop package contents.

## Snapshot And Incremental Ownership

Workshop metadata is a complete snapshot refresh. This allows removed or unlisted public records to disappear from the
next published index. Do not convert it to append-only accumulation without defining deletion and reconciliation
semantics.

Gallery and visual state are incremental. Resume them from the previously published Pages artifacts, prioritize changed
or failed records, backfill unknown records within the configured budget, and periodically refresh old records. An
absent previous corpus or an incompatible classifier or model version may require a full visual bootstrap.

Do not confuse reuse with permanence. A reused gallery or score still carries its source identity, collection or
classification version, coverage, and stale state so consumers can judge its quality.

## Steam And Resource Safety Controls

Treat the limits configured in the production and manual-backfill workflows as operational load and failure controls,
not as convenient performance knobs. This includes request batch sizes, sequential UGC behavior, daily and backfill
budgets, delays, image concurrency, refresh age, per-map image count, per-image size, retry behavior, and the public
artifact ceiling.

Do not increase those limits, shorten the refresh cadence, add broad retries, or classify additional item kinds merely
to finish a bootstrap faster. First identify which bounded resource is insufficient, estimate the additional Steam,
network, runner, model, and Pages load, and validate the new boundary through a manual GitHub-hosted run.

Keep Steam UGC requests sequential. After a failed UGC batch, stop before sending another one. For image downloads,
treat throttling and relevant server failures as circuit-breaker signals: stop queued work instead of continuing to
pressure Steam. Preserve the previous usable result as stale when the current workflow supports that fallback.

The workflow files are authoritative for the current numeric limits. When a reviewed limit changes, update nearby
technical documentation in the same change; do not maintain a second independent table of numbers here.

## Published Data Contract

Treat the GitHub Pages output as a public API. A normal consumer starts with `manifest.json` and
`search-index.jsonl.gz`. The remaining compressed artifacts expose the component metadata, gallery, and visual-feature
datasets for diagnostics or specialized consumers.

`tools/TimberbornMapPreviewClassifier/PUBLIC-DATA-CONTRACT.md` is authoritative for artifact purposes, record fields,
and consumer behavior. Preserve these compatibility rules:

- expose a public schema version separately from classifier and model versions;
- prefer additive fields;
- review every repository consumer before removing or renaming an artifact, field, feature key, or label, changing its
  type or meaning, or changing compression;
- use an explicit migration and schema-version change for an incompatible contract;
- make consumers tolerate unknown future fields, feature keys, and labels;
- retain numeric scores, aggregates, percentiles, coverage, version, and stale evidence even when friendly labels are
  added.

Do not couple a consumer to workflow internals or to today's exact set of visual criteria. A frontend or in-game search
consumer should be able to ignore an unfamiliar criterion while continuing to use the compatible fields it
understands.

## Interpreting Visual Evidence

Each public image is scored independently. Map-level median, mean, minimum, maximum, and spread describe different
aspects of the available image set. The median represents the predominant visual character; extrema may reveal a
feature visible in only part of the gallery.

CLIP similarities are relative ranking evidence, not probabilities. Percentiles are relative to the current published
map corpus and may move when that corpus changes. Labels are thresholded discovery aids, not verified statements about
terrain or geometry.

Search and UI copy must preserve those limits. Do not display a score such as `0.9` as "90% likely," and do not turn a
coarse label into an unqualified factual claim. Surface coverage or stale state when it materially affects user trust.

## Validation Decision Map

Choose validation according to the changed ownership surface:

- For classifier aggregation, percentile, stale/reuse, and public-schema generation changes, run focused local tests
  over deterministic fixtures.
- For a frontend or in-game consumer, test the documented schema version, missing optional data, stale records, and
  unknown future fields, feature keys, and labels.
- For Steam integration changes, use bounded read-only probes before a corpus run.
- For workflow, anonymous runtime, network, cache, concurrency, or Pages changes, run the manual GitHub-hosted workflow
  that exercises the real runner environment.
- For load-control changes, verify the intended bound and the stop behavior, not only successful throughput.

A green workflow job is necessary but not sufficient evidence of a healthy corpus. Inspect the published manifest and
logs for item and map coverage, classified/reused/missing/stale counts, gallery state, circuit-breaker failures,
classifier and schema versions, artifact sizes, Pages deployment, and action/runtime warnings. Compare those values
with the intended change and investigate unexplained regressions before calling the pipeline healthy.

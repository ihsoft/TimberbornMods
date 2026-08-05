# Timberborn Workshop Search Index Operational Knowledge

## Purpose

Use this knowledge when changing the public Timberborn Workshop search-index pipeline, its metadata and exact
payload-derived classifier tools, its GitHub Actions workflows, its published data contract, or a frontend or in-game
consumer of that data.

This document owns decision boundaries, safety controls, compatibility expectations, and validation strategy. Tool
commands and exact record fields belong in the closest tool README and the public data-contract document.

## System Boundary

The production pipeline has four distinct stages:

1. Build a complete anonymous HTTP snapshot of all publicly discoverable Timberborn Workshop item kinds.
2. Resume and inspect a bounded set of public map payloads for exact content-derived map metadata, using an anonymous
   Steam game-server UGC session.
3. Derive exact map classifications from the inspected payload metadata.
4. Merge and publish compact versioned artifacts through GitHub Pages.

Preserve these scope boundaries:

- metadata covers all discovered item kinds;
- payload inspection and exact classifications cover maps only;
- non-map search uses metadata, tags, and coarse category evidence, not map payload classifications;
- `preview_url` may be retained as public display metadata only;
- the indexer must not download or analyze preview or gallery images;
- Workshop package contents are inspected only by the reviewed bounded map-payload stage;
- downloaded map payloads are transient exact-metadata inputs and are not part of the published corpus.

Titles, descriptions, tags, categories, and exact payload-derived classifications are evidence for discovery. Exact
classifications describe only the implemented archive analysis, not full map quality, compatibility, or item behavior.

## Anonymous Execution Contract

The recurring pipeline must remain runnable on a clean GitHub-hosted runner without a Steam account, Steam login, API
key, repository secret, local Steam client, or Timberborn process.

The map metadata indexer is an intentional anonymous `SteamGameServerUGC` path. Do not replace it with authenticated
Steam-client access merely to obtain more data or reuse a familiar repository CLI pattern. Any proposal to introduce
account-bound access, credentials, a game launch, image downloads, gallery collection, or package downloads outside the
reviewed map metadata stage is an architecture and security change that requires explicit user approval after the
security and operations trade-offs are documented.

Read-only access is part of the contract. Metadata collection must not subscribe to items, mutate Workshop state,
download preview or gallery images, or download Workshop package contents. `SteamWorkshopMapMetadataIndexer` is the
narrow exception for package payloads: it may sequentially download one public Map-tagged payload per selected item,
within workflow size, item, timeout, and time budgets, to read exact map metadata and classifications. It must remain
resumable from previous results, preserve stale records when a refreshed item cannot be fetched, stop the pass after the
first failed payload request, and avoid publishing downloaded payload contents.

## Snapshot And Incremental Ownership

Workshop metadata is a complete snapshot refresh. This allows removed or unlisted public records to disappear from the
next published index. Do not convert it to append-only accumulation without defining deletion and reconciliation
semantics.

Map metadata state is incremental. Resume it from the previously published Pages artifact, prioritize changed, stale,
or unknown map records within the configured budget, and refresh records when the source Workshop timestamp or
`MapArchiveAnalyzer.AnalysisVersion` changes.

Do not confuse reuse with permanence. A reused exact map metadata record still carries its source identity, analysis
version, and collection state so consumers can judge its quality.

## Steam And Resource Safety Controls

Treat the limits configured in the production and manual-backfill workflows as operational load and failure controls,
not as convenient performance knobs. This includes request batch sizes, sequential UGC behavior, daily and backfill
budgets, payload item budgets, payload size caps, payload timeouts, retry behavior, and the public artifact ceiling.

Do not increase those limits, shorten the refresh cadence, add broad retries, or classify additional item kinds merely
to finish a bootstrap faster. First identify which bounded resource is insufficient, estimate the additional Steam,
network, runner, and Pages load, and validate the new boundary through a manual GitHub-hosted run.

Keep Steam UGC payload requests sequential. After a failed payload request, stop before sending another one. Preserve
the previous usable result as stale when the current workflow supports that fallback.

The workflow files are authoritative for the current numeric limits. When a reviewed limit changes, update nearby
technical documentation in the same change; do not maintain a second independent table of numbers here.

## Published Data Contract

Treat the GitHub Pages output as a public API. A normal consumer starts with `manifest.json` and
`search-index.jsonl.gz`. The remaining compressed artifacts expose the component Workshop metadata and exact map
metadata datasets for diagnostics or specialized consumers.

`tools/TimberbornMapPreviewClassifier/PUBLIC-DATA-CONTRACT.md` is authoritative for artifact purposes, record fields,
and consumer behavior. Preserve these compatibility rules:

- expose a public schema version separately from archive analysis and classifier versions;
- prefer additive fields;
- review every repository consumer before removing or renaming an artifact, field, feature key, or label, changing its
  type or meaning, or changing compression;
- use an explicit migration and schema-version change for an incompatible contract;
- make consumers tolerate unknown future fields, feature keys, and labels;
- retain exact map classification values, analysis version, and collection state so consumers can judge their quality.

Do not couple a consumer to workflow internals or to today's exact set of payload-derived criteria. A frontend or
in-game search consumer should be able to ignore an unfamiliar criterion while continuing to use the compatible fields
it understands.

## Validation Decision Map

Choose validation according to the changed ownership surface:

- For exact archive classifier, stale/reuse, and public-schema generation changes, run focused local tests over
  deterministic fixtures.
- For a frontend or in-game consumer, test the documented schema version, missing optional data, stale records, and
  unknown future fields, feature keys, and labels.
- For Steam integration changes, use bounded read-only probes before a corpus run.
- For workflow, anonymous runtime, network, cache, concurrency, or Pages changes, run the manual GitHub-hosted workflow
  that exercises the real runner environment.
- For load-control changes, verify the intended bound and the stop behavior, not only successful throughput.

A green workflow job is necessary but not sufficient evidence of a healthy corpus. Inspect the published manifest and
logs for item and map coverage, fetched/reused/missing/stale counts, payload request failures, analysis and schema
versions, artifact sizes, Pages deployment, and action/runtime warnings. Compare those values with the intended change
and investigate unexplained regressions before calling the pipeline healthy.

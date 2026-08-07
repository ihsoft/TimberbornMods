# Anonymous Workshop Map Analyzer

Downloads a bounded set of public Timberborn map payloads through an anonymous Steam game-server UGC session and
extracts exact map properties. It does not use a Steam account, API key, subscription, local Steam client, or game
process.

The tool incrementally reuses records from `--previous-results`. A map is analyzed again when its Workshop update
timestamp changes, its previous result is stale, or `MapArchiveAnalyzer.AnalysisVersion` increases. Steam requests are
sequential, and the first Steam/UGC request failure stops the pass before another request is sent. A downloaded payload
whose archive or map format cannot be analyzed is recorded as `unsupported`, and processing continues with the next
map. That result is retried only after the Workshop item or analysis version changes.

The narrow transient `k_EResultBusy`, `k_EResultNoConnection`, and `k_EResultFail` request results, plus timeouts waiting
for a Steam request callback, are retried twice: first after a 20-second cooldown and then after a 40-second cooldown.
They activate the circuit breaker only after those attempts are exhausted. Other Steam/UGC failures are not broadly
retried. If the pass stops early, previous records for selected but unprocessed maps remain unchanged in the checkpoint.

After either transient result, the sequential Steam request stream enters slow mode. `--slow-mode-delay-seconds`
controls its minimum request spacing and defaults to 15 seconds; the production workflow currently sets it to 40
seconds. Slow mode ends after six consecutive requests complete without another `Busy`, `NoConnection`, `Fail`, or
timeout; any of those failures resets that success count. With 40-second spacing, the first 20-second retry cooldown is
extended by the remaining 20 seconds, while the second 40-second cooldown is not extended.

## Private payload cache

The analyzer can retain exact downloaded `.timber` payloads in a private OCI artifact hosted by GitHub Container
Registry. Cached payloads are keyed by Workshop ID and source update timestamp, checked with SHA-256 when read, and
never included in public Pages artifacts. Configure the repository name to enable it:

```text
MAP_PAYLOAD_CACHE_OCI_REPOSITORY=ghcr.io/ihsoft/timberborn-workshop-payload-cache
```

The workflow installs ORAS, authenticates with its short-lived `GITHUB_TOKEN`, and requires `packages: write`; no
long-lived repository secret is needed. When the variable is absent, the analyzer keeps its previous no-cache behavior.
The package remains private and is not part of the public search index.

A failed GHCR blob write is retried after 20 and 40 seconds. If all attempts fail, the downloaded map is still analyzed
and the sequential Steam pass continues; the summary reports the cache write failure so a transient registry problem
does not masquerade as a map payload request failure or stop the indexing batch.

Final config, shard-manifest, and catalog writes use the same retry delays. If final cache publication still fails, the
analyzer writes and publishes its map metadata normally. The next run can therefore reuse the completed analysis and
refill only the missing cache entries instead of downloading the same maps as unprocessed analysis work.

After reading a complete Workshop snapshot, the analyzer removes every cache-catalog entry whose map ID is absent from
that snapshot. Only affected shard manifests and the catalog are republished; no payload download or analysis is
needed. After the public artifacts are prepared, the workflow also deletes untagged superseded GHCR package versions
up to the configured per-run limit, which defaults to 100. A larger limit can be supplied explicitly for temporary
backlog-cleanup runs. Current tagged catalog and shard versions remain intact. The
bounded cleanup resumes on later runs and is nonfatal to public-index publication if GitHub Packages temporarily
rejects listing or deletion.

Payloads are distributed across 100 stable logical shards using `published_file_id % 100`. Every map remains a separate
content-addressed OCI blob; a shard tag is only a small manifest referencing its blobs. Updating or adding a map uploads
only that map and rewrites the small shard manifest, never the other payload bytes. A separate catalog maps each
`(published_file_id, updated_at)` version to its shard, OCI digest, size, and SHA-256. The catalog is published last so
it never points to a shard version that has not been uploaded successfully.

Maps whose analysis version is stale and whose matching payload is cached are always processed first. They do not
consume `--max-items`, which is the per-run Steam download budget. After cached reanalysis, the analyzer downloads at
most that many missing or updated payloads. Analysis refreshes always precede background cache population. While the
cache is incomplete, otherwise up-to-date maps are gradually downloaded with the remaining budget so a future
classifier backfill can run from cached data. A failure during cache-only population preserves an already-current
metadata record unchanged instead of marking its valid analysis stale.

Cached payloads are fetched from GHCR and analyzed in parallel, with concurrency bounded by
`--max-analysis-parallelism` (four workers in the production workflow). This phase completes before the anonymous
Steam phase begins. Steam metadata queries and payload downloads remain strictly sequential, and cache writes happen
only in that sequential phase. A corrupt or unreadable cached payload affects only its own record and does not stop
other cached analyses.

`--request-delay-seconds` sets the minimum delay between sequential map payload requests and defaults to zero. Retry
cooldowns count toward that interval instead of being added to it, while slow mode raises the minimum to its own delay.

## Shared archive analysis

Each selected `.timber` ZIP is downloaded and opened once. `MapArchiveAnalyzer` reads authoritative runtime dimensions
from `world.json` under `Singletons.MapSize.Size`, falling back to `map_metadata.json` only for older payloads without
that singleton. It decodes shared map state once and passes it to the exact classifiers, which emit values under the
open `classifications` object.

To add another exact map criterion:

1. Implement a classifier with a normal `Analyze` method and without file or network access.
2. Invoke it from `MapArchiveAnalyzer.Analyze`, reusing already decoded map state where applicable.
3. Increment `MapArchiveAnalyzer.AnalysisVersion` so existing records are progressively backfilled.
4. Add focused archive fixtures and document the public result fields.

The `forest_density` classifier counts entities that yield `Log` unless `LivingNaturalResource.IsDead` is explicitly
`true`. A missing `LivingNaturalResource` component is the normal serialized form of a living tree.
Coverage is the living-tree count divided by land area after open surface-water tiles are excluded. Underground water
does not reduce land area. Levels `0` through `4` use the fixed bands `<5%`, `5–20%`, `20–35%`, `35–50%`, and `>50%`.

The `water` classifier decodes serialized surface-water columns and excludes water below the highest terrain surface.
It reports `open_water_tiles`, `open_water_ratio`, `lake_count`, and a searchable `water_form`: `none`, `rivers`,
`lakes`, or `rivers_and_lakes`. Water form combines local surface-level segmentation, boundary throughput, flow
coherence, shape, and the relative amount attributed to lake basins and river channels. It always chooses a concrete
form; internal ambiguous diagnostic regions are not published as a search value. Consumers can derive a
water-covered query directly from `open_water_ratio`, for example with a threshold greater than `0.4`.
For water reaching the map edge, a lake also needs a meaningful visible shoreline with exterior, edge-connected land.
Shorelines formed mostly by enclosed islands do not make the surrounding water a lake. A readable lake may still
extend beyond one edge of the map.

The `settlement_space` classifier estimates how much dry, sufficiently wide terrain is available for a settlement and
what shape dominates it. Isolated one- or two-tile height deviations are smoothed when their surrounding terrain
supports a one-level correction. Candidate regions need an interior core whose required radius grows sublinearly with
map size and is capped at five tiles. Non-overlapping cores measure absolute capacity; fewer than eight produces
`little_space`. Larger regions are classified from their elevation boundaries as plains, terraces, plateaus, or mixed
space. A dominant neighboring-height band may make disconnected dry regions a `plain`. The searchable `space_type` is
one of `little_space`, `much_space`, `plain`, `terraces`, or `plateau`; the result also reports `core_count` and the
capacity shares `plain_share`, `terrace_share`, `plateau_share`, and `mixed_share`.

## Output record

The JSONL output retains:

```text
published_file_id
source_updated_at_utc
analysis_version
map_width
map_height
classifications
collection_state
analysis_error
```

`collection_state` is `fetched`, `stale`, or `unsupported`. Unsupported records have zero dimensions, no
classifications, and a diagnostic `analysis_error`; consumers must not treat them as known map metadata.

`tools/TimberbornWorkshopSearchIndexPublisher/build_public_index.py` publishes classifications in merged search records as
`map_classifications`. Consumers must tolerate additional classifier keys and result fields.

Reviewed real-map water inputs are stored as compressed decoded fixtures in the focused test project. They preserve
the classifier inputs without redistributing complete Workshop payloads or requiring Steam during regression tests.

The production invocation and numeric Steam/resource limits remain authoritative in
`.github/workflows/workshop-search-index.yml`.

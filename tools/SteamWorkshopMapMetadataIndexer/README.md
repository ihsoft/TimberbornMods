# Anonymous Workshop Map Analyzer

Downloads a bounded set of public Timberborn map payloads through an anonymous Steam game-server UGC session and
extracts exact map properties. It does not use a Steam account, API key, subscription, local Steam client, or game
process.

The tool incrementally reuses records from `--previous-results`. A map is analyzed again when its Workshop update
timestamp changes, its previous result is stale, or `MapArchiveAnalyzer.AnalysisVersion` increases. Steam requests are
sequential, and the first Steam/UGC request failure stops the pass before another request is sent. A downloaded payload
whose archive or map format cannot be analyzed is recorded as `unsupported`, and processing continues with the next
map. That result is retried only after the Workshop item or analysis version changes.

## Shared archive analysis

Each selected `.timber` ZIP is downloaded and opened once. `MapArchiveAnalyzer` reads authoritative runtime dimensions
from `world.json` under `Singletons.MapSize.Size`, falling back to `map_metadata.json` only for older payloads without
that singleton. It then makes one pass through the `world.json` entity array. Registered `IMapEntityClassifier`
instances observe that shared entity stream and independently emit values under the open `classifications` object.

To add another exact map criterion:

1. Implement `IMapEntityClassifier` without performing file or network access.
2. Register its factory in `MapArchiveAnalyzer.ClassifierFactories`.
3. Increment `MapArchiveAnalyzer.AnalysisVersion` so existing records are progressively backfilled.
4. Add focused archive fixtures and document the public result fields.

The `forest_density` classifier counts entities that yield `Log` and whose
`LivingNaturalResource.IsDead` value is not `true`. Missing `IsDead` is the normal serialized form of a living tree.
Coverage is the living-tree count divided by map width multiplied by map height. Levels `0` through `4` use the fixed
bands `<5%`, `5–20%`, `20–35%`, `35–50%`, and `>50%`.

The `water` classifier decodes serialized surface-water columns and excludes water below the highest terrain surface.
It reports `open_water_tiles`, `open_water_ratio`, `lake_count`, and a searchable `water_form`: `none`, `rivers`,
`lakes`, or `rivers_and_lakes`. Water form combines local surface-level segmentation, boundary throughput, flow
coherence, shape, and the relative amount attributed to lake basins and river channels. It always chooses a concrete
form; internal ambiguous diagnostic regions are not published as a search value. Consumers can derive a
water-covered query directly from `open_water_ratio`, for example with a threshold greater than `0.4`.

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

`tools/TimberbornMapPreviewClassifier/build_public_index.py` publishes classifications in merged search records as
`map_classifications`. Consumers must tolerate additional classifier keys and result fields.

The production invocation and numeric Steam/resource limits remain authoritative in
`.github/workflows/workshop-search-index.yml`.

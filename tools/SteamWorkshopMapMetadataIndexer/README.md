# Anonymous Workshop Map Analyzer

Downloads a bounded set of public Timberborn map payloads through an anonymous Steam game-server UGC session and
extracts exact map properties. It does not use a Steam account, API key, subscription, local Steam client, or game
process.

The tool incrementally reuses records from `--previous-results`. A map is analyzed again when its Workshop update
timestamp changes, its previous result is stale, or `MapArchiveAnalyzer.AnalysisVersion` increases. Steam requests are
sequential, and the first failed payload stops the pass before another request is sent.

## Shared archive analysis

Each selected `.timber` ZIP is downloaded and opened once. `MapArchiveAnalyzer` reads dimensions from
`map_metadata.json`, then makes one pass through the `world.json` entity array. Registered `IMapEntityClassifier`
instances observe that shared entity stream and independently emit values under the open `classifications` object.

To add another exact map criterion:

1. Implement `IMapEntityClassifier` without performing file or network access.
2. Register its factory in `MapArchiveAnalyzer.ClassifierFactories`.
3. Increment `MapArchiveAnalyzer.AnalysisVersion` so existing records are progressively backfilled.
4. Add focused archive fixtures and document the public result fields.

The initial `forest_density` classifier counts entities that yield `Log` and whose
`LivingNaturalResource.IsDead` value is not `true`. Missing `IsDead` is the normal serialized form of a living tree.
Coverage is the living-tree count divided by map width multiplied by map height. Levels `0` through `4` use the fixed
bands `<5%`, `5–20%`, `20–35%`, `35–50%`, and `>50%`.

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
```

`tools/TimberbornMapPreviewClassifier/build_public_index.py` publishes classifications in merged search records as
`map_classifications`. Consumers must tolerate additional classifier keys and result fields.

The production invocation and numeric Steam/resource limits remain authoritative in
`.github/workflows/workshop-search-index.yml`.

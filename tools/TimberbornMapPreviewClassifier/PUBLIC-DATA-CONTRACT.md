# Public Workshop Search Data Contract

The GitHub Pages output is a public, account-independent search dataset. A normal consumer starts with:

1. `manifest.json` to check schema compatibility, freshness, coverage, and file sizes.
2. `search-index.jsonl.gz` as the merged record stream for search and display.

## Artifacts

`manifest.json` contains the integer `schema_version`. Consumers should ignore unknown additive fields. Removing or
renaming a field or artifact, changing its meaning, or changing compression or record format requires an explicit
schema migration and consumer review.

The manifest `files` object lists these UTF-8 JSON Lines gzip artifacts:

- `workshop-items.jsonl.gz` — complete current public Workshop metadata;
- `map-metadata.jsonl.gz` — incremental exact dimensions and payload-derived map classifications;
- `search-index.jsonl.gz` — the merged consumer stream.

Schema version `2` removes the gallery and image-classification artifacts and their merged fields. The index was not
yet consumed by a released MapBrowser version, so no compatibility bridge is published.

## Search-index scope

Workshop metadata covers every discovered public item kind. Exact map fields are present only for records whose Steam
tag list contains `Map` and whose payload has been inspected. Titles and descriptions do not determine map scope.

Merged records retain public title, description, tags, author, timestamps, votes, category evidence, and primary
`preview_url`. The URL is metadata only; the indexing pipeline does not download or analyze the image. Inspected map
records may additionally contain `map_width`, `map_height`, `map_analysis_version`,
`map_metadata_collection_state`, and `map_classifications`.

`map_metadata_collection_state` may be `fetched`, `stale`, or `unsupported`. An unsupported record can expose
`map_analysis_error` for diagnostics but does not expose dimensions or classifications. It is retried when its source
Workshop timestamp or archive analysis version changes.

Map dimensions come from the payload's runtime `world.json` map size. `map_metadata.json` is used only as a fallback
for older payloads that do not serialize that runtime singleton.

`map_classifications` is an open object. Consumers must tolerate future classifier keys and fields.

The `forest_density` result contains `live_tree_count`, `coverage_ratio`, and an integer `level` from `0` through `4`.
It counts initial living entities that yield logs and divides the count by land area after open surface-water tiles are
excluded. Its fixed bands are `<5%`, `5–20%`, `20–35%`, `35–50%`, and `>50%`.

The `water` result contains `open_water_tiles`, `open_water_ratio`, `lake_count`, and `water_form`. The form is one of
`none`, `rivers`, `lakes`, or `rivers_and_lakes`. Consumers can derive water-covered searches directly from
`open_water_ratio`, for example with a threshold greater than `0.4`.

The `plateaus` result contains `plateau_count`, `plateau_land_ratio`, and `plateau_level`. Only sufficiently wide,
connected, dry terrain regions count as plateaus; coverage uses their complete area divided by land area. The level is
one of `few_plateaus`, `has_plateaus`, `many_plateaus`, or `flat_map`. A flat map may contain disconnected regions or
regions on neighboring heights, including land separated by open water.

## Anonymous execution boundary

The recurring pipeline requires no Steam account, API key, repository secret, local Steam client, or running game.
The bounded map-payload stage uses an anonymous Steam game-server session. Payloads are transient analysis inputs and
are not part of the published corpus.

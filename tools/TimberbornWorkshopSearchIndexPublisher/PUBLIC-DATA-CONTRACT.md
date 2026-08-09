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

Schema version `2` is the current exact-payload schema consumed by MapBrowser.

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
It counts initial entities that yield logs unless they are explicitly marked dead, and divides the count by land area
after open surface-water tiles are excluded. Its fixed bands are `<5%`, `5–20%`, `20–35%`, `35–50%`, and `>50%`.

The `water` result contains `open_water_tiles`, `open_water_ratio`, `broad_boundary_water_ratio`,
`largest_water_body_ratio`, `lake_count`, and `water_form`. The largest-water-body ratio is the share of the full map
occupied by the largest four-way-connected open-water body. The form is one of `none`, `rivers`, `lakes`, or
`rivers_and_lakes`. A water-covered search uses open water above 40 percent plus either broad water on at least half
the perimeter or a largest connected water body covering at least 45 percent of the map.

The `settlement_space` result contains `core_count`, `plain_share`, `terrace_share`, `plateau_share`, `mixed_share`,
and `space_type`. Only sufficiently wide, dry terrain regions contribute non-overlapping capacity cores. The searchable
type is one of `little_space`, `much_space`, `plain`, `terraces`, or `plateau`. A plain may contain disconnected regions or
regions on neighboring heights, including land separated by open water.

The `islands` result is a descending array of integer projected dry-land areas. Terrain height does not affect an
island's area. River-separated pieces may be merged into one island, while land surrounded by a lake is not treated as
an island. Internal water must also be substantial relative to the enclosed land, so a narrow moat does not turn most
of a map into an island. An empty array means the map was analyzed and no useful islands were found. A missing
`islands` key means the value is unknown and must not be interpreted as an empty result.

The `canyons` result is an array of connected canyon systems. Each entry contains `length`, `average_width`, and
`median_bank_height`, measured in projected map tiles and terrain levels. Detection is based on continuous confined
terrain corridors; saved surface water supports the interpretation but is not required. Broad valleys, isolated
basins, map-scale open water, highly cyclic pits, and geometrically perfect trenches are rejected. An empty array means
the map was analyzed and no canyon was found. A missing `canyons` key means the value is unknown.

The `mountains` result is a descending array of non-overlapping projected mountain areas in map tiles. Mountains are
identified from locally prominent summits and their key saddles. Minor summits on a shared base are absorbed into the
dominant mountain, while independently prominent summits divide the shared projection without double-counting it.
Candidates dominated by abrupt terrain edges or by descent into an enclosed canyon-like depression are rejected. An
empty array means the map was analyzed and no useful mountains were found. A missing `mountains` key means the value
is unknown.

## Anonymous execution boundary

The recurring pipeline requires no Steam account, API key, repository secret, local Steam client, or running game.
The bounded map-payload stage uses an anonymous Steam game-server session. Payloads are transient analysis inputs and
are not part of the published corpus.

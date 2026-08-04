# Public Workshop Search Data Contract

The GitHub Pages output is a public, account-independent search dataset. A normal consumer starts with:

1. `manifest.json` to check compatibility, freshness, coverage, and file sizes.
2. `search-index.jsonl.gz` as the merged record stream for search and display.

Consumers should not depend on workflow working directories, diagnostic artifacts, or retained source images.

## Manifest compatibility

`manifest.json` contains an integer `schema_version`. Schema version `1` is the initial public contract generation.
This is separate from:

- `visual_classifier_version`, which identifies feature extraction and aggregation behavior;
- `visual_model`, which identifies the ML model used for image scoring.

A classifier or model change does not by itself rename the public schema. Conversely, schema compatibility must not be
inferred from an unchanged classifier version.

Schema evolution should be additive whenever possible. Consumers should ignore unknown fields, visual feature keys,
and labels so newly added criteria remain compatible. Removing or renaming an artifact, field, feature key, or label,
changing its meaning, or changing the compression/record format requires an explicit schema migration and consumer
review.

The manifest `files` object lists the canonical compressed data artifacts and their byte sizes:

- `workshop-items.jsonl.gz` — the complete current public Workshop metadata snapshot;
- `map-gallery.jsonl.gz` — incremental map gallery URL state;
- `map-metadata.jsonl.gz` — incremental exact dimensions and content-derived map classifications;
- `map-visual-features.jsonl.gz` — map-only visual profiles and image coverage;
- `search-index.jsonl.gz` — metadata enriched with available gallery and visual fields; the normal consumer stream.

Each compressed artifact is UTF-8 JSON Lines inside gzip: one independent JSON object per non-empty line.

## Search-index scope

Workshop metadata covers all discovered public item kinds. Gallery and visual fields are present only when applicable;
visual classification intentionally covers records categorized as maps. Consumers must not describe non-map items as
visually classified merely because they appear in `search-index.jsonl.gz`.

The merged records retain public title, description, tags, author, timestamps, votes, category evidence, primary
preview URL, and any known gallery URLs. Map records may additionally contain:

- `map_width`, `map_height`, `map_analysis_version`, and `map_classifications` from inspected map payloads;
- `visual_scores` and `visual_percentiles` for the median map profile;
- `visual_score_aggregates` and `visual_percentile_aggregates` with `median`, `mean`, `min`, `max`, and `spread`;
- `visual_labels` for coarse discovery;
- `visual_image_count`, `visual_gallery_image_count`, and `visual_missing_image_count` for coverage;
- `visual_stale`, `visual_model`, and `visual_classifier_version` for quality and provenance.

Fields can be absent when no visual or gallery result exists. Consumers must distinguish absence from a numeric zero,
and should surface or filter stale and incomplete coverage when accuracy matters.

`map_classifications` is an open object keyed by content-derived classifier name. The `forest_density` result contains
`live_tree_count`, `coverage_ratio`, and an integer `level` from `0` through `4`. It counts initial map entities that
yield logs and whose `LivingNaturalResource.IsDead` value is not `true`, then divides that count by `map_width`
multiplied by `map_height`. Its fixed bands are `<5%`, `5–20%`, `20–35%`, `35–50%`, and `>50%`. Consumers must tolerate
unknown future classifier keys and fields.

## Interpreting visual criteria

Visual scores are CLIP similarity-derived ranking evidence, not probabilities and not verified map geometry. Percentiles
are relative to the complete map corpus of the current generated snapshot; the same raw score can receive a different
percentile as the corpus changes.

The median describes what predominantly characterizes the available images. `max` can help find a feature visible in
only part of a gallery, while `spread` indicates disagreement across images. Labels are thresholded discovery aids and
must not be presented as proof of exact counts, dimensions, resources, hazards, or gameplay behavior.

Schema version 1 publishes these visual feature keys:

- `ruggedness`
- `canyonness`
- `water_dominance`
- `islandness`
- `forest_density`
- `artificial_layout`

It may publish these labels:

- `predominantly_mountainous`
- `predominantly_flat`
- `canyon_or_narrow_valley`
- `water_dominated`
- `islands`
- `densely_forested`
- `artificial_layout`

These lists are not closed enums for consumers. Future additive criteria and labels may appear without a schema-major
change.

## Images and account boundary

Visual profiles are computed from the public primary preview and up to eight valid public gallery images per map.
Images are transient inputs and are not retained in the published corpus. A separate bounded pass inspects downloaded
public map payloads for exact content-derived classifications. Both paths remain anonymous and require no Steam account,
API key, repository secret, local Steam client, or running game.

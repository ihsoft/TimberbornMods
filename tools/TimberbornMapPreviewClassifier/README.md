# Timberborn Map Preview Classifier

Builds search-oriented visual terrain features from public primary previews and Workshop gallery screenshots collected
by `tools/SteamWorkshopIndexer` and `tools/SteamWorkshopGalleryIndexer`. It does not read Workshop package contents,
use a Steam account, or use titles and descriptions as classifier inputs.

The classifier uses CLIP prompt pairs to produce relative visual scores for:

- rugged or mountainous terrain;
- canyons and narrow valleys;
- islands;
- artificial or geometric layouts.

Raw CLIP similarities are not probabilities. Each feature uses four fixed score thresholds to produce an absolute
level from `0` through `4`. A map's level therefore depends only on its own previews and the classifier version; adding
other maps to the index cannot change it. Thresholds freeze the final five score buckets of the v2 corpus until those
features receive human calibration. Preview-derived `forest_density` and `water_dominance` are legacy fields: new
visual classifications no longer compute them because exact payload-derived classifiers supersede them. Existing
public fields and calibration utilities remain for compatibility and historical analysis.

`normalize_map_preview.py` is an experimental diagnostic, not part of the published classifier pipeline. It requires
`requirements-experiments.txt`. Edge-aware masking can remove border-connected sky in unusual previews such as
`Painting Wall`, but evaluation on the 50-map water calibration set found that masking, cropping, and approximate
perspective rectification all reduced threshold stability. Perspective normalization is therefore deliberately not
applied during production classification.

## Legacy forest and water calibration sets

Create a local, human-labeled reference set without manually searching Workshop maps:

```powershell
python tools/TimberbornMapPreviewClassifier/create_forest_calibration_set.py
```

These utilities describe the retired preview-derived signals and are not part of current production classification.
The forest script reads a compatible published index, selects 50 maps deterministically across the full score range,
includes Workshop item `3619540066` (`4 Point`) as a known false-negative candidate, downloads the selected primary
previews, and writes the ignored local review page to:

```text
.tools/map-vision/forest-calibration/index.html
```

Open the page locally and assign one of five forest-density levels with keys `1` through `5`. Progress is saved in
browser local storage. Use **Export labels.json** when finished; the exported file contains the original raw score and
legacy percentile beside each human label so fixed score thresholds can be calibrated. Count only living green trees
as forest; dead or dry trees do not count. Judge density relative to the map's
total area rather than by absolute tree count: the same number of trees represents a lower density on a larger map.
Treat the five levels as a practical Timberborn map-density scale: the highest level means the densest realistic forest
presence in a map preview, not that trees cover the entire map area.

Create the analogous water-availability reference set with:

```powershell
python tools/TimberbornMapPreviewClassifier/create_water_calibration_set.py
```

Its review page is written to `.tools/map-vision/water-calibration/index.html`. Blue free-water areas are the primary
contribution; green irrigated or moist soil contributes less but remains meaningful. Both are judged relative to total
map area rather than by absolute tile count. Workshop items `3672607632` (`001-Musje`) and `3652824726` (`00100`) are
always included as known low-score maps with substantial visible moist ground. Forest and water pages use independent
browser storage and export files.

Each image is scored independently. The published map profile retains per-feature `median`, `mean`, `min`, `max`, and
`spread` aggregates, fixed `visual_levels`, and image coverage. The median remains the `visual_scores` value and answers
questions about what predominantly characterizes a map; extrema and spread support future deterministic search for
features that appear only in part of a map. These ready-made numeric parameters are intended to remain usable by an
in-game search mod that cannot run the ML model.

## Local setup

Keep dependencies and model files under ignored `.tools/map-vision`; do not install them system-wide. The validated
bootstrap uses Python 3.12, CPU-only PyTorch 2.7.1, Transformers 4.53.2, and
`openai/clip-vit-base-patch32`.

The setup is intentionally not automatic because downloading dependencies and model weights requires operator
approval.

## Future GPU acceleration

The classifier can use a CUDA-enabled PyTorch build. The workstation used for the bootstrap has an NVIDIA GeForce GTX
1050 with 4 GB of VRAM and a driver supporting CUDA 12.6. The initial corpus run intentionally uses CPU-only PyTorch:
the CUDA package would push the local Workshop and model data beyond the agreed 10 GB ceiling, while the available
VRAM would still require conservative batch sizes. Revisit GPU execution when storage policy or hardware changes; do
not install a CUDA build automatically.

## Run

Set `PYTHONPATH` to the local dependency folder and `HF_HOME` to the local model cache, then run:

```powershell
python tools/TimberbornMapPreviewClassifier/classify.py
```

Default input and output:

```text
.tools/workshop-index/timberborn-workshop-bootstrap.jsonl
.tools/workshop-index/previews/<published-file-id>.preview
.tools/workshop-index/timberborn-map-visual-features.jsonl
```

Use `--max-items` for a bounded calibration run and `--batch-size` to tune CPU and memory usage.

## Scheduled public index

`.github/workflows/workshop-search-index.yml` runs manually or daily. It collects a complete Workshop metadata snapshot
without a preview cache. An anonymous Steam game-server UGC query reads additional preview URLs in batches of up to 100
map IDs without opening individual Workshop HTML pages. The bounded pass prioritizes changed or failed maps, gradually
backfills unknown maps, and periodically refreshes old results. The classifier reuses the previous raw score for every
unchanged image URL, so PyTorch and CLIP are needed only for newly discovered or changed images. The job recomputes map
aggregates and fixed absolute levels and publishes compact GitHub Pages artifacts:

```text
manifest.json
workshop-items.jsonl.gz
map-gallery.jsonl.gz
map-visual-features.jsonl.gz
search-index.jsonl.gz
```

The merged search index retains public primary and gallery URLs so an agent can inspect final candidates without
retaining the image corpus. At most eight resized gallery screenshots are considered per map, each downloaded image is
limited to 2 MB, the public artifact is limited to 100 MB, and images are discarded after their scores are computed.
The workflow uses no Steam account, API key, repository secret, or game process.

The workflow also downloads a bounded number of map payloads through an anonymous Steam game-server session. A single
archive analysis reads exact dimensions from `map_metadata.json` and feeds the entities in `world.json` to registered
content classifiers. `map-metadata.jsonl.gz` retains dimensions plus exact initial living-tree and open-water
classifications. Adding another content classifier extends this shared archive analysis instead of introducing another
Workshop download or map scanner. Records are reused until the
Workshop item's update timestamp or the map-analysis version changes, so scheduled runs progressively backfill new
classifications without repeatedly downloading already complete maps.

Content-derived forest and water classifications replace the legacy preview-derived `forest_density` and
`water_dominance` signals. The visual values may remain in older or partially migrated records for compatibility, but
the current visual classifier does not generate them.

Frontend and other data consumers should use the versioned contract in
[`PUBLIC-DATA-CONTRACT.md`](PUBLIC-DATA-CONTRACT.md).

The daily gallery pass processes at most 250 maps in up to three sequential UGC requests, with a short delay between
batches. This bound controls new image downloads and CPU classification cost rather than Steam HTML throttling. The
pass checks changed or previously failed items first, backfills recent unknown items next, and refreshes known galleries
after 90 days. It initializes SteamCMD only as an ephemeral anonymous runtime on the GitHub runner; it does not use a
Steam client login, API key, repository secret, or game process.

`.github/workflows/workshop-gallery-backfill.yml` is a manual-only accelerator that runs the same publishing pipeline
for at most 1,000 maps. It keeps UGC requests sequential, waits one second between batches, limits image downloads to
two concurrent requests, and shares the daily job's concurrency lock. The gallery indexer stops before sending another
UGC request after the first failed batch. The classifier does not retry Steam HTTP 403, 429, or server errors and stops
queued downloads before starting its next image batch, so a throttled or unhealthy backfill fails visibly instead of
continuing to pressure Steam.

The published `manifest.json` reports how many maps were classified, reused, missing, or served with stale scores. If
an updated preview cannot be downloaded after retries, the previous score is retained as stale and retried on the next
run. A missing previous index, model change, or classifier-version change automatically falls back to a full visual
bootstrap.

Classifier-version migrations are resumable. The scheduled workflow gives classification a bounded runtime and writes
the completed new-version records together with untouched prior-version records. Each record retains its own
`classifier_version`, so the next run reuses completed work and continues only the remaining maps. During migration the
manifest reports `visual_classifier_version: mixed`, the target version, and completed/remaining map and image counts.
Old-version records may still contain legacy percentiles during migration so released MapBrowser builds can read the
mixed snapshot. New-version records contain fixed `visual_levels`; no cross-map percentile recalculation is performed.

GitHub Pages must be configured to use **GitHub Actions** as its deployment source before the first deployment.

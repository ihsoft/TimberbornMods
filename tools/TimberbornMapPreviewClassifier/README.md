# Timberborn Map Preview Classifier

Builds search-oriented visual terrain features from public primary previews and Workshop gallery screenshots collected by
`tools/SteamWorkshopIndexer`. It does not read Workshop package contents, use a Steam account, or use titles and
descriptions as classifier inputs.

The classifier uses CLIP prompt pairs to produce relative visual scores for:

- rugged or mountainous terrain;
- canyons and narrow valleys;
- water-dominated maps;
- islands;
- forest density;
- artificial or geometric layouts.

Raw CLIP similarities are not probabilities. The tool converts each score into a percentile within the current map
corpus. Search should rank by these percentiles and treat labels as coarse discovery aids, not verified map geometry.

Each image is scored independently. The published map profile retains per-feature `median`, `mean`, `min`, `max`, and
`spread` aggregates, their corpus-relative 0–1 percentiles, and image coverage. The median remains the backwards-
compatible `visual_scores` value and answers questions about what predominantly characterizes a map; extrema and spread
support future deterministic search for features that appear only in part of a map. These ready-made numeric parameters
are intended to remain usable by an in-game search mod that cannot run the ML model.

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

Use `--max-items` for a bounded calibration run and `--batch-size` to tune CPU and memory usage. The full run is a
snapshot operation because percentiles depend on the complete current corpus.

## Scheduled public index

`.github/workflows/workshop-search-index.yml` runs manually or daily. It collects a complete Workshop metadata snapshot
without a preview cache. A bounded gallery pass prioritizes changed maps and then gradually backfills or periodically
refreshes older public item pages. Steam throttling or the time budget defers remaining pages to later runs. The
classifier reuses the previous raw score for every unchanged image URL, so PyTorch and CLIP are needed only for newly
discovered or changed images. The job recomputes map aggregates and corpus-relative percentiles and publishes compact
GitHub Pages artifacts:

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

The daily gallery pass attempts at most 300 item pages and stops after 20 minutes or three consecutive failures. It
checks changed items first, backfills recent unknown items next, and refreshes known galleries after 90 days.

The published `manifest.json` reports how many maps were classified, reused, missing, or served with stale scores. If
an updated preview cannot be downloaded after retries, the previous score is retained as stale and retried on the next
run. A missing previous index, model change, or classifier-version change automatically falls back to a full visual
bootstrap.

GitHub Pages must be configured to use **GitHub Actions** as its deployment source before the first deployment.

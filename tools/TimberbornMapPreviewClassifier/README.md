# Timberborn Map Preview Classifier

Builds search-oriented visual terrain features from the public preview cache created by
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

`.github/workflows/workshop-search-index.yml` runs manually or every Monday. It collects Workshop metadata without a
preview cache, downloads only the current classification batch, and publishes compact GitHub Pages artifacts:

```text
manifest.json
workshop-items.jsonl.gz
map-visual-features.jsonl.gz
search-index.jsonl.gz
```

The merged search index retains public preview URLs so an agent can visually inspect a few final candidates without
retaining the complete image corpus. The workflow uses no Steam account, API key, repository secret, or game process.

GitHub Pages must be configured to use **GitHub Actions** as its deployment source before the first deployment.

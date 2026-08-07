# Timberborn Workshop Search Index Publisher

Builds compact public search artifacts from the complete public Workshop metadata snapshot and progressively inspected
map payloads. It does not download or analyze preview or gallery images. The public primary `preview_url` is retained
as ordinary Steam metadata for consumers that choose to display it.

## Inputs

- `workshop-items.jsonl` contains the complete current public Workshop catalog.
- `map-metadata.jsonl` contains resumable exact map dimensions and payload-derived classifications.

`build_public_index.py` merges these inputs and writes:

```text
manifest.json
workshop-items.jsonl.gz
map-metadata.jsonl.gz
search-index.jsonl.gz
index.html
```

Run from the repository root:

```powershell
python tools/TimberbornWorkshopSearchIndexPublisher/build_public_index.py `
  --snapshot .work/workshop-items.jsonl `
  --map-metadata .work/map-metadata.jsonl `
  --output-directory .work/public
```

The scheduled workflow remains anonymous and read-only. It enumerates public Workshop metadata over HTTP, then uses an
anonymous Steam game-server session only for the bounded, resumable map-payload stage. Downloaded payloads are transient
inputs and are not published.

Frontend and other data consumers should follow
[`PUBLIC-DATA-CONTRACT.md`](PUBLIC-DATA-CONTRACT.md).

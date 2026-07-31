# Anonymous Workshop Gallery Indexer

Collects public additional-preview URLs for Timberborn maps through anonymous Steam game-server UGC queries. The tool
is read-only and account-independent: it does not use a Steam client login, API key, repository secret, subscription,
Workshop package download, or Timberborn process.

The input is a JSONL snapshot produced by `tools/SteamWorkshopIndexer`. Only records whose `primary_category` is
`map` are considered. The output is JSONL with one record per known map:

```text
published_file_id
source_updated_at_utc
gallery_checked_at_utc
gallery_urls
gallery_images_found
gallery_truncated
collection_state
```

Only absolute HTTPS image URLs hosted by `images.steamusercontent.com` are retained. URLs are normalized to a bounded
preview size.

## Build and run

The native Steam game-server runtime must be available to Steamworks.NET. The GitHub workflow installs an ephemeral
SteamCMD runtime on its Linux runner; no login is performed.

From the repository root:

```powershell
dotnet build tools/SteamWorkshopGalleryIndexer/SteamWorkshopGalleryIndexer.csproj -c Release
dotnet tools/SteamWorkshopGalleryIndexer/bin/Release/net8.0/SteamWorkshopGalleryIndexer.dll `
  --snapshot .work/workshop-items.jsonl `
  --previous-results .work/previous-map-gallery.jsonl.gz `
  --output .work/map-gallery.jsonl `
  --batch-size 100 `
  --max-items 250 `
  --max-images-per-map 8 `
  --delay-milliseconds 250
```

Options:

- `--snapshot` and `--output` are required.
- `--previous-results` accepts an existing plain or gzip JSONL result for incremental reuse.
- `--batch-size` is limited to 1–100 maps.
- `--max-images-per-map` is limited to 1–32 retained URLs.
- `--refresh-after-days` controls periodic refresh of otherwise unchanged records; the default is 90.
- `--delay-milliseconds` adds a delay between sequential UGC batches.
- `--max-items` bounds records refreshed in one run; zero means no item bound.

## Incremental lifecycle and failure behavior

Changed and previously failed maps are selected first, unknown maps next, and expired known records last. Records not
selected in the current bounded run are copied forward with `collection_state` set to `reused`. A successful query uses
`fetched`; a missing response is recorded as `stale`, preserving the previous URLs when available.

UGC requests are sequential. If a batch fails, the tool throws before sending another Steam request and does not write
a partially advanced output. At least one successful batch with usable map details is required whenever candidates
were selected. These bounds and stop conditions protect the anonymous recurring job; changes to them are load and
safety changes, not routine performance tuning.

The scheduled and manual pipelines are documented in
`tools/TimberbornMapPreviewClassifier/README.md`. The published frontend contract is
`tools/TimberbornMapPreviewClassifier/PUBLIC-DATA-CONTRACT.md`.

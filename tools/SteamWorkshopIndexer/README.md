# Public Steam Workshop Indexer

Creates a read-only Timberborn Workshop snapshot using only anonymously accessible HTTP resources. It does not require
Steam, a Steam account, an API key, or Timberborn. It never subscribes to items and cannot download Workshop package
contents.

The job reads public Workshop browse pages to enumerate published file IDs, resolves metadata through the public
`ISteamRemoteStorage/GetPublishedFileDetails` endpoint, and caches each item's public primary preview. The preview cache
is intended as input for a separate visual map classifier.

Build and run from the repository root:

```powershell
dotnet build tools/SteamWorkshopIndexer/SteamWorkshopIndexer.csproj -c Release
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll
```

Default ignored outputs:

```text
.tools/workshop-index/timberborn-workshop-bootstrap.jsonl
.tools/workshop-index/timberborn-workshop-bootstrap.summary.json
.tools/workshop-index/previews/<published-file-id>.preview
```

The JSONL record contains public metadata, normalized description text, Steam tags, a coarse content-kind
classification, the public preview URL, and the relative preview-cache path. Descriptions remain evidence, not ground
truth for terrain classification.

## Periodic job behavior

Run the command without `--append` from a scheduler as a complete snapshot refresh. This removes records that are no
longer present in the public Workshop catalog. An unchanged preview URL with an existing cache file is not downloaded
again.

Use `--append` only for bounded chunked or resumed runs. It preserves records outside the pages processed by the
current invocation, including records that may since have disappeared from Workshop.

Use bounded runs for diagnostics or resumable chunks:

```powershell
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll `
  --start-page 1 --max-pages 40 --append
```

The public browse page currently contains 30 results per page. The crawler reads Steam's embedded public
`total_count` value and stops after the final page. It also stops when a page yields no new IDs.

## Disk and request controls

The default preview-cache ceiling is 8,000,000,000 bytes. The job stops before crossing it. Raising the ceiling must be
an explicit operator choice:

```powershell
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll `
  --append --max-preview-cache-bytes 8000000000
```

Other useful options:

- `--skip-previews` indexes metadata without downloading images.
- `--preview-directory <directory>` changes the image cache location.
- `--delay-ms <milliseconds>` controls the polite delay between public requests; the default is 150 ms.
- `--preview-concurrency <1-16>` limits parallel preview downloads; the default is 6.
- `--output <jsonl>` changes the snapshot location.

This is a bootstrap/full-refresh job. A later incremental layer can stop after a stable overlap window because browse
results are ordered by last update, but that optimization is intentionally outside the current contract.

Additional gallery screenshots are not returned by the anonymous batch details endpoint. The scheduled search-index
workflow collects their URLs separately with `tools/TimberbornMapPreviewClassifier/collect_gallery.py`. That collector
uses bounded, delayed public item-page requests and carries its published state between runs.

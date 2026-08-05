# Public Steam Workshop Indexer

Creates a read-only Timberborn Workshop snapshot using only anonymously accessible HTTP resources. It does not require
Steam, a Steam account, an API key, or Timberborn. It never subscribes to items and cannot download Workshop package
contents.

The job reads public Workshop browse pages to enumerate published file IDs, resolves metadata through the public
`ISteamRemoteStorage/GetPublishedFileDetails` endpoint. It retains each item's public primary-preview URL as metadata,
but never downloads or analyzes the image.

Build and run from the repository root:

```powershell
dotnet build tools/SteamWorkshopIndexer/SteamWorkshopIndexer.csproj -c Release
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll
```

Default ignored outputs:

```text
.tools/workshop-index/timberborn-workshop-bootstrap.jsonl
.tools/workshop-index/timberborn-workshop-bootstrap.summary.json
```

The JSONL record contains public metadata, normalized description text, Steam tags, a coarse content-kind
classification and the public preview URL. An item is classified as a map when its
Steam tag list contains `Map`; any other tags may coexist and do not affect that decision. Title and description text
are never evidence that an item is a map. Descriptions remain weaker evidence for other coarse content kinds and
search, not ground truth for terrain classification.

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

The crawler requests 50 results per browse page through Steam's supported `num_per_page=50` parameter, reads the
embedded public `total_count` value, and stops after the final page. It also stops when a page yields no new IDs.
Browse IDs are accumulated across pages and resolved through `GetPublishedFileDetails` in batches of up to 100, reducing
the number of metadata requests without increasing the number of Workshop items processed.

## Request controls

- `--delay-ms <milliseconds>` controls the polite delay between public requests; the default is 150 ms.
- `--output <jsonl>` changes the snapshot location.

This is a bootstrap/full-refresh job. A later incremental layer can stop after a stable overlap window because browse
results are ordered by last update, but that optimization is intentionally outside the current contract.

The index retains the public primary preview URL as metadata but does not download or analyze preview or gallery
images.

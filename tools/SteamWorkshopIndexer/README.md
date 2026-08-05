# Public Steam Workshop Indexer

Creates a read-only Timberborn Workshop snapshot through an anonymous Steam game-server connection. It does not require
a Steam account, an API key, or Timberborn. It never subscribes to items and does not download Workshop package
contents.

The job enumerates public items and reads their metadata through `SteamGameServerUGC`. It retains each item's public
primary-preview URL as metadata, but never downloads or analyzes the image. A Steam client runtime must be discoverable
by the process; the scheduled workflow installs an anonymous SteamCMD runtime before starting the indexer.

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

Every run creates a complete snapshot refresh, removing records no longer present in the public Workshop catalog.
Steam returns at most 50 results per page. The indexer checks that the reported total remains stable, rejects duplicate
IDs, and publishes output only after it has collected exactly the reported number of items.

## Request controls

- `--output <jsonl>` changes the snapshot location.
- `--request-timeout-seconds <seconds>` changes the callback timeout; the default is 120 seconds.

Transient `k_EResultBusy` and `k_EResultNoConnection` query results are retried twice after a 10-second cooldown. Error
logs include the page, Steam result, callback I/O failure reason, login state, cache flag, and returned/total counts.

The index retains the public primary preview URL as metadata but does not download or analyze preview or gallery
images.

# Steam Workshop Indexer

Creates a read-only bootstrap snapshot of public Timberborn Workshop metadata. It does not subscribe to items, download
their package content, update Workshop metadata, or start Timberborn. A running and logged-in Steam client is required.

Build and run from the repository root:

```powershell
dotnet build tools/SteamWorkshopIndexer/SteamWorkshopIndexer.csproj -c Release
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll
```

The default outputs are ignored local artifacts:

```text
.tools/workshop-index/timberborn-workshop-bootstrap.jsonl
.tools/workshop-index/timberborn-workshop-bootstrap.summary.json
```

Each JSONL record preserves the raw Steam description, a plain-text search form, Steam tags, timestamps, voting data,
and an evidence-bearing bootstrap classification. The categories are deliberately simple and are not a semantic model.

Steamworks queries may become unstable during a long process. Use bounded runs to resume in chunks:

```powershell
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll --start-page 1 --max-pages 40
dotnet tools/SteamWorkshopIndexer/bin/Release/net8.0/SteamWorkshopIndexer.dll `
  --start-page 41 --max-pages 40 --append
```

`--append` loads the existing JSONL file, merges new records by published file ID, and rewrites a deduplicated snapshot.
Steam currently returns up to 50 items per page. Use `--language` with a Steam API language name to request another
localized title and description.

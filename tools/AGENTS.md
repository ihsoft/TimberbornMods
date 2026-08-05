# Repository Tool Instructions

These instructions apply to standalone repository tools under `tools/`. They complement the root `AGENTS.md` and do
not relax its role, safety, publication, or external-state gates.

## Repository Coordination

Use `repository-coordination.psm1` for tracked tools that need exclusive ownership of a shared local resource. Do not
invent tool-specific lock files or delete diagnostic records directly.

Every tool that launches an agent-controlled Unity Editor or batch process for this repository's work must hold the
main repository's `unity-project` lock for the entire operation, including when it opens a temporary or separately
cloned project. Tools that own a complete Git staging and commit transaction must hold `git-transaction`. The lock does
not authorize the underlying Unity, Git, release, or external-state operation.

Keep the existing native safety checks. Unity project lockfiles/process checks and Git's `.git/index.lock` catch
non-participating processes and low-level collisions; repository coordination prevents the higher-level race between
otherwise valid checks and commands.

## Authenticated Standalone Steamworks.NET Tools

For a standalone Steamworks.NET CLI that uses the logged-in Steam client, treat the CLI as a repository tool, not as
the Timberborn game process.

- Require a running, logged-in Steam client before calling Steamworks APIs.
- Set both `SteamAppId` and `SteamGameId` to the intended application ID before `SteamAPI.Init()` so the tool does not
  depend on `steam_appid.txt` being present in the process working directory.
- Do not call `SteamAPI.RestartAppIfNecessary()` unless the tool is intentionally designed to run as the game through
  Steam. Metadata query, indexing, and repository-maintenance tools must not start Timberborn merely to initialize
  Steamworks.

Successful Steamworks initialization does not authorize Workshop changes. Read-only tools must remain read-only, and
tools that mutate Steam state still require the explicit user authorization and release or publishing gates applicable
to that operation.

## Anonymous Workshop Map Metadata Indexing

`SteamWorkshopMapMetadataIndexer` is intentionally not an authenticated Steam-client CLI. It uses an ephemeral
anonymous Steam game-server session and `SteamGameServerUGC` to download a bounded set of public Map-tagged Workshop
payloads for exact metadata inspection. Do not apply the logged-in-client initialization contract above to this tool.

Keep this path read-only, account-independent, non-subscribing, and non-game-launching. It must not require a Steam
account, client login, API key, repository secret, local Steam client, or Timberborn process. It may inspect downloaded
map payload contents only inside the narrow bounds documented in
`docs/agent-knowledge/Timberborn-Workshop-Search-Index-Operational-Knowledge-v1.md`; do not reintroduce gallery
collection, preview downloading, image classification, or other package-download stages without explicit user approval.

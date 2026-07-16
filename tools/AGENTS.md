# Repository Tool Instructions

These instructions apply to standalone repository tools under `tools/`. They complement the root `AGENTS.md` and do
not relax its role, safety, publication, or external-state gates.

## Standalone Steamworks.NET Tools

Treat a standalone Steamworks.NET CLI as a repository tool, not as the Timberborn game process.

- Require a running, logged-in Steam client before calling Steamworks APIs.
- Set both `SteamAppId` and `SteamGameId` to the intended application ID before `SteamAPI.Init()` so the tool does not
  depend on `steam_appid.txt` being present in the process working directory.
- Do not call `SteamAPI.RestartAppIfNecessary()` unless the tool is intentionally designed to run as the game through
  Steam. Metadata query, indexing, and repository-maintenance tools must not start Timberborn merely to initialize
  Steamworks.

Successful Steamworks initialization does not authorize Workshop changes. Read-only tools must remain read-only, and
tools that mutate Steam state still require the explicit user authorization and release or publishing gates applicable
to that operation.

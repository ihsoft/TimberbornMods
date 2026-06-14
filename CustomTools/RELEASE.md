# CustomTools release workflow

This is the pilot release workflow for one mod. If it stays useful, apply the same shape to the other mods.

## Build the package

From the repository root:

```powershell
.\tools\build-mod-package.ps1 -ModName CustomTools
```

The script:

1. Reads `CustomTools/Mod/manifest.json`.
2. Reads `CustomTools/directory.build.props`.
3. Fails if the manifest version and assembly version differ.
4. Builds `CustomTools/CustomTools.csproj` in `Release`.
5. Creates `.tools/release-staging/CustomTools/CustomTools/version-1.1`.
6. Copies `CustomTools/Mod` into that game-version folder.
7. Copies `CustomTools.dll` and optional `CustomTools.xml` into `Scripts`.
8. Writes `_MODS!/CustomTools_v<version>.zip`.

Before writing the zip, the script validates the staged package:

- at least one `version-X.X` folder must exist;
- every `version-X.X` folder must contain `manifest.json`;
- every `version-X.X` folder must contain `Scripts/CustomTools.dll`;
- every `version-X.X` folder must contain `Scripts/CustomTools.xml`.

Use `-Force` only when intentionally replacing an existing local package.

```powershell
.\tools\build-mod-package.ps1 -ModName CustomTools -Force
```

## Legacy game-version folders

The repository currently contains the source for the active `version-1.1` folder. Existing release artifacts may also
contain older folders, such as `version-1.0`.

To include locally staged legacy folders from `_MODS!/CustomTools`, run:

```powershell
.\tools\build-mod-package.ps1 -ModName CustomTools -IncludeLegacyVersions
```

This copies any `version-*` folders except the active one from `_MODS!/CustomTools` into the package. Do not rely on
this for a clean machine unless the legacy folders have already been restored locally.

Use `-LocalModRoot <path>` when the local Steam/legacy staging folder is somewhere else.

## Steam Workshop

Use the generated `_MODS!/CustomTools_v<version>.zip` as the release package. The local `_MODS!/CustomTools` folder may
also contain `workshop_data.json`, which the package builder copies into the zip when present.

## Mod.IO

Prepare a Mod.IO publish plan:

```powershell
.\tools\publish-modio.ps1 -ModName CustomTools -IncludeLegacyVersions
```

This is a dry run. It builds the package, reads the actual zip, generates the Mod.IO change notes with the game-version
compatibility suffix, and prints the upload endpoint when local configuration is available.

Use `-ChangeNotesPrefix "..."` to test or add a temporary line at the beginning of the Mod.IO change notes without
editing `CHANGES.md`.

Real publishing is only allowed after an explicit user request to publish:

```powershell
.\tools\publish-modio.ps1 -ModName CustomTools -IncludeLegacyVersions -Publish
```

If automating this later, use the Mod.IO REST API instead of storing credentials in the repository:

- API keys and OAuth tokens must come from environment variables or ignored files under `.tools/`.
- Mod.IO write operations require an OAuth2 bearer access token.
- Adding a new modfile is `POST /games/:game-id/mods/:mod-id/files`.
- Editing mod metadata is a separate `POST /games/:game-id/mods/:mod-id` call.

Relevant documentation:

- <https://docs.mod.io/restapi/introduction>
- <https://docs.mod.io/restapi/docs/add-modfile>
- <https://docs.mod.io/restapi/docs/edit-mod>

Local Mod.IO configuration belongs in `.tools/modio/CustomTools.local.json`:

```json
{
  "ApiBase": "https://g-<game-id>.modapi.io/v1",
  "GameId": "<game-id>",
  "ModId": "<mod-id>"
}
```

The OAuth token must be supplied through the ignored local file:

```text
.tools/modio/CustomTools.token.txt
```

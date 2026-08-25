# Release readiness and signoff artifacts

Routine release work starts with one read-only discovery command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/get-release-readiness.ps1 -ModName <ModName>
```

For a registered compatibility line, also pass `-ReleaseLine <ReleaseLine>`. The command resolves the registered
worktree from `tools/release-lines.json`; it does not accept or persist a machine-local compatibility-worktree path.
`-AsJson` emits the same report as compact JSON. The saved schema-1 artifact lives under
`.tools/release-readiness/` and contains no credential values.

Readiness is discovery only. It reads release configuration, Git, local package state, Unity installation state,
GitHub CLI auth state, and platform prerequisite presence. It does not edit release metadata, build, export, package,
authenticate interactively, or mutate a public platform. `Gate1Plan` is the prepared argument and operation inventory
for `tools/verify-release.ps1`; the preflight requires the matching ready artifact.

Steam publication uses the Steamworks-documented `steamcmd +login <configured-user> +workshop_build_item ... +quit`
shape. The tooling has no `logout` or `logoff` command; `quit` terminates SteamCMD. Readiness reports whether the
desktop Steam client is running so a Publisher can correlate any client or Steam Cloud disruption with the concurrent
SteamCMD login. The tooling never stops or restarts the desktop client.

After `tools/publish-release.ps1` completes or is interrupted after loading a valid preflight, it writes a schema-1
artifact under `.tools/release-signoff/`. The artifact projects immutable schema-5 preflight evidence plus publish
operation records. It contains package/source hashes, lane fingerprints, platform/tag/GitHub evidence, final Mod.IO
readback, issue and Wiki evidence candidates, Discord state, log paths, partial-success state, and an incident
inventory. Full child logs remain under `.tools/release-logs/`.

Issue and Wiki results are evidence candidates only. The tools do not close issues or edit Wiki pages. Discord sending
also remains manual. Every signoff artifact sets `FullySignedOff` to false and leaves `MentorOutcome` as `Pending` or
`Unresolved`; a human must record the final Mentor decision before treating the release as completely signed off.

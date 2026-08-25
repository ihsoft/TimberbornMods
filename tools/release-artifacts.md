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
for `tools/verify-release.ps1`. A registered older-line candidate may first be `ReadyForGate1` with
`PreparationRequired`: candidate and previous source versions come from that line's changelog, while the public package
version and platform identity come from the current public owner in the main worktree. The report lists exact stale
metadata changes. After the authorized preparation commit, rerun the same readiness command; immutable preflight
requires `ReadyForPreflight` and never accepts the pre-preparation artifact.

GitHub readiness is evidence from the process's current credential-access boundary. It runs read-only `gh auth status`
and `gh api user` checks without trying to escape that boundary. A failure, including HTTP 401, means auth/API access
is unavailable to that readiness process; it does not by itself prove that the Windows Credential Manager secret is
invalid. Before logout, token deletion, or device reauthentication, rerun the same readiness command through the
approved release execution boundary. GitHub Desktop and `gh` remain separate sessions.

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

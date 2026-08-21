# Timberborn Release Publishing Rules for AI Agents

Use these rules when preparing, validating, or publishing Timberborn mod releases to Steam Workshop or Mod.IO.

Publishing is a high-risk workflow. Prefer stopping and asking over guessing.

For clean-checkout local environment requirements such as `_MODS!`, `_GAME!`, dependency links, Unity, SteamCMD, Mod.IO
tokens, GitHub CLI auth, generated references, and local release artifacts, follow
`docs/timberborn-new-repository-bootstrap-for-ai-agents.md`.

## Conditional Release Knowledge

This file is the routine release runbook. Load additional release knowledge only when its condition applies:

| Condition | Read |
|---|---|
| The mod lacks a verified Steam `PublishedFileId` or Mod.IO `ModId` | `docs/agent-knowledge/Timberborn-Release-Identity-Bootstrap-Operational-Knowledge-v1.md` |
| Same-version corrective replacement, unchanged legacy-lane restoration, or post-release artifact comparison | `docs/agent-knowledge/Timberborn-Release-Recovery-Operational-Knowledge-v1.md` |
| Changing or diagnosing platform tags, visibility, descriptions, change notes, Private item verification, or Steam authentication | `docs/agent-knowledge/Timberborn-Release-Platform-Metadata-Operational-Knowledge-v1.md` |
| Platform uploads are verified, or the task performs post-release tag, GitHub Release, issue, Wiki, Discord, or final signoff work | `docs/agent-knowledge/Timberborn-Release-Signoff-Operational-Knowledge-v1.md` |

Do not load every conditional document preemptively. The core rules below preserve the mandatory routine gates and
identify when a conditional branch must be loaded.

## Explicit publish requests only

Never publish to Steam or Mod.IO unless the user explicitly asks to publish.

Dry runs may build packages, validate files, generate metadata, and prepare staging directories. They must not upload
anything.

## Publisher task boundary

Run each release, or one tightly related release batch, in a fresh Publisher task when practical. Keep preparation,
final preflight, Steam and Mod.IO publication, live verification, Git tag, GitHub Release, issue audit or closure
decision, Wiki handoff, Discord handoff, and final signoff in that same task. If a platform operation partially succeeds
or its outcome is uncertain, keep diagnosis and recovery in the same task so the mutation boundary is not lost. Do not
split one release into separate platform tasks.

Reconstruct release context from the current checkout and durable release artifacts: applicable root and local rules,
the target changelog and release configuration, current Git history and tags, package state, preflight reports, and
saved release logs. Do not read old Publisher conversations by default. Consult a previous task only when a specific
fact cannot be recovered safely from authoritative repository, report, log, or platform evidence.

Keep cross-release tooling work and broad platform investigations in separate Publisher-tooling tasks. A persistent
Publisher task may be used as a lightweight coordination inbox for Mentor and role notifications, but should not be the
default place to execute releases. Do not depend on a fixed task title or thread ID; the role assignment and target
release establish the task's identity.

## Routine Black-Box Execution

For a routine release, treat the committed unified release scripts as the execution interface. Read their compact
plans, reports, hashes, warnings, and mutation results; do not inspect script source, enumerate broad `tools/` content,
or load complete successful logs merely to re-derive checks the scripts already performed.

Open a child script, detailed log, or conditional release document only when a command fails, a plan differs from the
expected release, a required invariant is absent from the compact report, or the task explicitly changes tooling or
platform metadata. Keep the investigation scoped to that discrepancy. Successful black-box execution does not weaken
any explicit authorization, immutable-snapshot, live-verification, or partial-success gate in this runbook.

## Unified preflight and publish scripts

Prefer the unified release entry points when they support the target release:

- `tools/verify-release.ps1` for local release preparation and verification.
- `tools/publish-release.ps1` for public publishing from a preflight report.

`tools/verify-release.ps1` is the preflight step. It may create or refresh local generated release artifacts such as
Unity exports, `_MODS!` output, staging folders, ZIPs, VDFs, temporary GitHub release notes, and
`.tools/release-preflight/<ModName>-<Version>.json` reports when those artifacts are needed to validate the exact
package that would be published. It must not pass `-Publish` to platform scripts or change public platform state.

`tools/publish-release.ps1` consumes a preflight report and performs the public publish step. It must re-check critical
preflight invariants before public changes, including the release identity and source/package state captured by the
report. It may request required host or network elevation up front when practical.

Use the unified scripts' compact child output for routine releases. Every distinct child invocation must remain visible
as a named operation so repeated export, build, package, verification, or publish work is not hidden. Use
`-DetailedOutput` only when the user requests it or when diagnosing a failure or meaningful plan difference.

Preserve complete stdout and stderr for every child operation under the ignored
`.tools/release-logs/<ModName>-<Version>/` tree and record the relevant log paths in the preflight report. Do not load
complete success logs into agent context by default.

The compact summary must still surface release identity, version and commit, compatibility lane, package and source
hashes or fingerprints, visibility intent, platform IDs, tag equality or mutations, public mutation results, actionable
warnings, and log paths. On child failure, show the complete child output and saved log path while preserving a nonzero
exit. If public steps already completed, report them explicitly together with partial-success state and any unknown
external outcome; do not collapse the result into a generic failure.

Issue closing and Wiki handoff remain explicit user-confirmed follow-up steps, not automatic side effects of the
publish script.

The Discord release handoff is a local preparation step after successful publication. It may open the target channel,
but it must not copy the proposed message to the clipboard, send through, or automate the user's Discord account. It
does not depend on GitHub Release creation.

Treat unexpected unified-script behavior as a stop-and-investigate condition instead of silently falling back to ad hoc
manual steps.

## Final preflight snapshot

Treat a final preflight report marked ready for publishing as the immutable release snapshot, not merely as evidence
that an earlier package happened to pass validation. Before the report captures source fingerprints and package hashes,
the preflight workflow must materialize every derived release input needed by any requested platform, including root
metadata and compatibility tags, then build and validate the final package from that completed source.

The report must also capture the exact Git object ID of the selected mod's release-preparation commit. Do not infer this
identity from whatever `HEAD` happens to be current when final preflight or publishing runs. When later commits already
exist, verify that changes after the selected release commit do not alter that mod's implementation, release metadata,
package source, or a shared release-critical dependency used by the artifact. If the correct release commit is
ambiguous, or later changes affect its release inputs, stop and obtain an explicit commit selection or prepare a new
release commit before final preflight.

Before marking the report ready, compare every tracked release-critical input used by the build or publication with the
selected release-preparation commit. Final preflight must fail when implementation, package source, changelog, version
metadata, Unity manifest data, platform descriptions, or other tracked release inputs differ in the working tree. Do
not build or publish from those uncommitted values and then tag the older commit. Commit the complete release
preparation, select that commit, and rerun final preflight.

The unified preflight report must record the selected commit and the checked release-critical path set or equivalent
evidence that those inputs match it. `tools/publish-release.ps1` must re-check that invariant before its first public
change and fail on drift; a warning or interactive "continue anyway" prompt is not sufficient for this condition.

When publishing includes an explicitly authorized platform-state transition, the final preflight report must capture
the action and exact target state for each platform, including Steam visibility and Mod.IO page publication. Publishing
must consume that recorded intent rather than infer it from local metadata or make a new publish-time choice. Stop if
the user authorization, preflight report, and publish command do not agree.

After that snapshot is captured, publishing must not mutate the configured package source, update its
`workshop_data.json` or tags, rebuild or repackage the release, or substitute a different package. Each platform
publisher must consume the package captured by final preflight, or staging derived from that exact package, and must
re-check the applicable source fingerprint and package hash before its first public change. A child publish script must
fail instead of applying a local metadata update or selecting a code path that reconstructs the package after the
snapshot.

If publishing discovers that derived metadata, tags, package contents, or platform staging inputs still need to change,
stop before the next public change. Apply the change during release preparation or preflight, rebuild the package,
repeat all affected validation, and create a new final preflight report with the new fingerprints and hashes. Do not
continue under the old report even when the rebuilt package is expected to be equivalent.

## Exceptional Corrective Releases

Do not use the routine workflow to replace already published contents under the same mod version. This requires
explicit user authorization for the named mod, version, and platforms plus the Release Recovery Operational Knowledge.
Do not infer corrective-replacement mode from a packaging defect or an existing platform release.

## Network checks

Release publishing requires live Steam, GitHub, and Mod.IO checks.

In this repository environment, PowerShell network requests from the sandbox may fail with connection-closed errors.
For mandatory release checks or upload verification, request an escalated run instead of treating the sandbox network
failure as evidence about the platform state.

Do not skip required platform verification because a sandboxed network request failed.

For publishing tasks, these are expected host or network operations and may require an escalated run when the sandbox
cannot access the real host state: real `_MODS!` build and inspection, Steam dry runs and uploads, Mod.IO dry runs and
uploads, platform-description verification, GitHub issue search/comment/close, and post-upload Mod.IO parent or live
file checks.

Keep elevation scoped to the release workflow step that needs it. Ordinary repository reads, local diffs, and local
planning do not need elevation only because the task is a release.

Prefer grouping read-only post-upload Mod.IO parent, file, virus-scan, platform-status, and live checks into one
verification command when practical. Do not split them into several approvals unless a follow-up check depends on the
previous result.

## Unknown external outcomes

Treat a timeout, interruption, lost command output, or ambiguous exit after a publish operation starts as an unknown
external outcome, not as proof that the operation failed before mutation. Before retrying, perform read-only checks of
every external boundary the operation may have reached, such as Steam item and content state, Mod.IO files and page
state, Git tags, and GitHub Releases. Establish the last confirmed mutation, then retry only the incomplete step. If the
boundary cannot be established reliably, stop and report the uncertainty instead of retrying.

## Dirty worktree before publishing

Before uploading a mod release, check for uncommitted changes that belong to the target mod or to shared
release-critical dependencies.

Uncommitted changes in unrelated mods do not block publishing.

For the target mod, treat these uncommitted changes as a red flag:

- files under the target mod directory,
- the target mod's test project,
- `ModsUnityProject/Assets/Mods/<ModName>/`,
- the target mod's `Workshop` files, changelog, release metadata, and package source tracked in Git,
- release scripts, release configs, or shared publishing tools changed for the current release.

For any mod release, also treat uncommitted changes in `TimberDev` or `TimberDev.Tests` as a red flag because
TimberDev is shared by many mods.

Uncommitted tracked release-critical inputs are a hard stop for final preflight and publication, not an overridable red
flag. This includes changes that affect the selected mod's implementation, package contents, changelog, version
metadata, manifest, platform descriptions, package construction, or release-tool behavior. Preliminary investigation
may report such changes, but a publish-ready report must not capture values absent from the selected committed
revision. User authorization to publish does not substitute for committing the release preparation.

When other red-flag changes are present but do not affect the committed release snapshot:

1. Stop before upload.
2. List the specific files or file groups.
3. Ask the user one direct question: whether to continue publishing despite those uncommitted changes.
4. Do not fix, stage, commit, revert, or otherwise clean up those changes as part of the publish flow unless the user
   explicitly asks.

Uncommitted changes in unrelated mods may be mentioned as background, but they should not block publishing.

For shared docs/tools changes, block only when they affect the current publish flow, release validation, release
metadata, package construction, or platform upload. Otherwise, mention them as background instead of stopping.

## Publish script sequencing

Do not run Steam and Mod.IO release scripts for the same mod in parallel.

The publish scripts may share staging and output paths, such as `.tools/release-staging/<ModName>-local` and generated
ZIP paths under the local mod source or output folder. Run dry runs and uploads sequentially unless the scripts have
been changed to use unique staging roots per invocation.

If stale Mod.IO or Steam staging output blocks packaging, it is safe to clear only the target mod's own staging
directory, such as `.tools/release-staging/<ModName>-local`, after resolving the path and verifying that it is inside
`.tools/release-staging`. Do not clean broader staging or output directories as a convenience.

Recommended order:

1. Steam dry run.
2. Mod.IO dry run.
3. Steam upload, if requested.
4. Mod.IO upload, if requested.

## Release-tooling fixes during publishing

If a publish task exposes a bug in release helper scripts or tooling, prefer one verified tooling commit after the
helper works end-to-end. Avoid committing each intermediate failed attempt while diagnosing the helper under live
release pressure.

Checkpoint commits are acceptable only when the user explicitly asks for them or when a working partial fix must be
preserved before a risky operation. Otherwise, finish the helper fix, rerun the relevant dry run or verification, then
commit the tooling change.

## Unpublished versions

When the user asks to publish all unpublished mod versions, treat "unpublished versions" as package changelog sections
still marked `(TBD)`.

Search package changelogs such as `CHANGELOG.md` and `CHANGES.md` for top-level version sections marked `(TBD)`. Those
sections are the publish candidates. If no `(TBD)` sections exist, report that there are no unpublished mod versions to
publish.

Here `(TBD)` identifies a release candidate for preparation; it does not mean that the candidate is ready for final
preflight or upload.

Do not interpret "unpublished versions" as missing Git tags, missing GitHub Releases, missing platform artifacts, or
historical release backfill. Do not create historical tags or GitHub Releases for already-published versions unless the
user explicitly asks for tag or GitHub Release backfill.

## Exact version matching

When the user names a release version, it must exactly match the configured release version.

Examples:

- If the user asks for `Automation 1.4.0` and the release config says `4.1.0`, stop.
- If the user asks for `Automation 4.1` and the release config says `4.1.0`, stop.

Do not treat close versions as aliases. Ask the user to confirm the exact version.

Before release preparation, verify that the target package changelog has a matching version section and user-visible
release notes. Changelog workflow rules live in `docs/timberborn-repository-notes.md`.

If the requested version matches the top package changelog section and that section is marked `(TBD)`, treat stale
release metadata as normal release-preparation work. Update `release.json`, `directory.build.props`, Unity manifest
versions, and other release metadata to the requested version as needed instead of stopping only because they still
contain the previous published version.

Before the release-preparation commit and final unified preflight, replace `(TBD)` in the selected version heading with
the concrete release date using that changelog's established format. The dated changelog must be part of the committed
Git revision captured by final preflight so the eventual release tag points to published history rather than an
unpublished marker. A selected heading that still contains `(TBD)`, or otherwise lacks its required concrete release
date, is a stop condition for final preflight, upload, release creation, and tagging. Preliminary dry runs may happen
earlier, but their reports are not publish-ready snapshots; rerun final preflight after dating and committing the
changelog.

This release-preparation allowance does not weaken the upload gate. Before any real upload, the release config,
generated package path, final package contents, and newly built current compatibility lane must match the requested
version exactly. A deliberately preserved legacy lane may retain its previously published DLL and manifest versions
only through the verified legacy-lane workflow below, and its manifest must match the lane-specific value in
`ManifestVersions`. Stop if the current lane, package path, or package source would publish an older artifact, such as a
previous-version ZIP.

## Target game version

At the start of release preparation, clarify which Timberborn game version folder the release targets, such as
`version-1.1`, unless the user already stated it.

If the user does not name a specific game version folder, treat the intended default as the current Timberborn game
version, meaning the latest known game version for the current checkout and release tooling. Resolve that current
version from repository evidence such as `release.json`, Unity export configuration, package source contents, or the
release scripts, then state the concrete `version-X.X` value before changing version files, exporting Unity assets, or
building DLLs.

Do not hardcode a previously used game version folder just because it appears in a project file, post-build target, old
package, or previous release. If the resolved current game version conflicts with any build target, manifest mapping,
Unity export output, or package source folder, stop and ask instead of silently building or packaging for the older
folder.

## Exact source paths only

If `release.json` points to `Package.SourcePath`, use only that exact path.

If it does not exist, stop. Do not:

- search for similar folders,
- use `*-bak` folders,
- substitute previous ZIP files,
- infer a different source folder from timestamps or names.

Tell the user to restore the exact source path, rebuild/export the mod to the exact path, or explicitly update
`release.json`.

## Package source mode

For a newly prepared release of a Unity-exported mod, prefer `Package.Mode = "LocalModFolder"` when an up-to-date
`_MODS!/<ModName>` source folder exists. Build, validate, and package that source folder instead of relying on a
previously generated ZIP.

`Package.Mode = "ExistingZip"` is acceptable only when the user explicitly asks to publish that specific ready ZIP, or
when the release config already points to a fresh ZIP for the requested version and the package has been validated.
Do not publish an older ZIP just because it is still referenced by `release.json`.

## Source folder is truth

For `Package.Mode = "LocalModFolder"`, the source folder is the source of truth.

The configured source folder is the only input for release package contents. Agents may validate it, package it, and
report what is missing. Agents must not repair, supplement, or reinterpret it using previous archives, backup folders,
similarly named folders, or inferred state except for the narrowly verified unchanged-legacy-lane restoration below.

Treat `version-*` folders as compatibility lanes, not as mandatory folders for every Timberborn game version. Add or
export a new `version-X.X` folder only when the release needs a distinct compatibility lane, such as stable versus
experimental or an incompatible game API/data change. If the source folder does not contain a game-version folder, the
release does not contain it.

## Building LocalModFolder Releases

If a release intentionally restores an unchanged legacy compatibility lane, load and follow Release Recovery
Operational Knowledge before copying that lane. Never obtain the current lane from a previous artifact.

For LocalModFolder releases with Unity-exported assets, use this order:

1. Update repository version files first, including `release.json`, `directory.build.props`, and Unity manifest data as
   needed.
2. Run the Unity export for the selected compatibility lane, so the exported source folder and `version-X.X` lane are
   refreshed or created from current Unity assets.
3. For a release-capable source, verify the official exporter materialized root `workshop_data.json` and any required
   `thumbnail.jpg` from the selected current lane. Before intentional release-tool metadata changes, the root files
   must match that lane byte-for-byte. Do not hand-copy them or source them from a legacy lane.
4. Build the C# project with `ModPath` pointing at the exported mod root.
5. Verify that the expected DLL/XML were copied into the selected lane's `Scripts` folder and that the DLL assembly
   version matches the intended release.
6. If the release intentionally preserves an unchanged legacy lane, restore it through the verified workflow above
   after current-lane export and build, then validate the completed source folder.

Root metadata materialization does not authorize Steam visibility changes. Normal preflight must continue to report no
visibility update unless the user explicitly authorized that exact change through the visibility workflow below.

Before building a release ZIP from `Package.SourcePath`, make sure the current code has been built into that exact
source folder. For C# mods, run the mod project build with the real `ModPath` and verify that the expected DLL/XML
files in the source folder were updated. Do not package a local mod folder that may contain stale binaries.

Resolve `ModPath` according to the mod project's own build contract. Many project-local package projects append
`$(ProjectName)` and the compatibility lane under `$(ModPath)`, so they expect `ModPath` to be the mods root, not the
target mod folder. If a build creates `<ModName>/<ModName>/version-*` under the configured release source, treat that as
wrong build output, remove only the verified generated nested folder, rebuild with the correct `ModPath`, and verify the
final package source contains no nested duplicate mod folder before packaging.

Before building the release binary, update the mod's `directory.build.props`. The DLL assembly version is taken from
that file. Verify the built DLL assembly version after the build. Other version declarations still matter for their own
consumers: the Unity `manifest.json` version is used by the game, and `release.json` is used by the release process.

If `directory.build.props` was updated after a build or package dry run, treat the previous DLL and ZIP as stale and
rebuild/repackage before publishing.

If the release package is built from a local Unity-exported mod folder, do not rely on a manual "Unity was exported"
assumption when release tooling can perform the export. Export the selected compatibility lane before the release C#
build when possible. If export tooling is unavailable, ask the user to confirm that Unity assets were exported before
building the ZIP. Do this before packaging, because missing or stale asset bundle changes cannot be detected reliably
from C# build output.

Before running `tools/export-unity-mod.ps1` or a release script that invokes Unity batch export, make sure the regular
Unity Editor is not open on the same `ModsUnityProject`. Current export tooling should preflight this, but keep the
rule as release workflow guidance for older checkouts and any other Unity batch invocation path.

If Unity batch export exits with code 1 and the per-export log is very short, only shows startup/licensing/project-open
output, or ends before `ModBuilderBatch` logs an exception, first check whether a regular Unity Editor process is
already open on the project. Close that editor and retry the export once before deeper investigation. If the retry still
fails, continue normal Unity diagnostics: the per-export log, global Unity `Editor.log`, project compile or import
errors, mod registration, and tooling or upstream changes.

After the user confirms that Unity assets were exported, verify the `manifest.json` version inside the configured
`Package.SourcePath` or final package source. Do not rely only on the Unity project file, because the Unity project and
the exported package source can be out of sync.

## Package validation

Before any upload, validate the final package, not just source files.

Every package must contain at least one `version-X.X` folder.

Every `version-X.X` folder must contain:

- `manifest.json`,
- `Scripts/<ScriptFileBase>.dll`,
- `Scripts/<ScriptFileBase>.xml`.

Missing XML is allowed only for explicit legacy exceptions listed in the release config for specific version folders,
after the user confirms that case. Keep XML required by default; do not add a global exception for a mod or release.

If `ManifestVersions` is configured, each manifest version must match its configured version for that folder.

## Routine Platform Verification Gates

The compact preflight and publish plans must show platform IDs, compatibility tags, description equality, visibility
intent, and Mod.IO file/version intent. Stop if any of those fields is absent or differs from the requested release.

Derive compatibility claims and update tags from the final package's actual `version-*` lanes plus explicit documented
release-config exceptions. Do not infer support for an absent lane. Verify exact target/live tags through the supported
platform tooling before and after any required synchronization.

Normal release publishing must not change Steam visibility. A local visibility value is not authorization. Stop if a
routine plan includes a visibility mutation; any intentional change requires explicit user authorization and Platform
Metadata Operational Knowledge.

Verify the target mod's tracked Steam and Mod.IO descriptions against live platform text before upload. A mismatch for
the target platform is a stop condition, not permission to update either side. Load Platform Metadata Operational
Knowledge only when metadata must change, verification fails, a Private item needs owner-only handling, or Steam
authentication requires diagnosis.

For Mod.IO, use the target mod's config and a known valid owner token, generate compatibility metadata from the final
package, and verify that the uploaded file becomes the live file with the intended version. A different known owner
token file for the same account and game scope may be passed explicitly; never copy, rename, or guess token files.

## Signoff Transition

After every requested platform upload and live verification succeeds, load Release Signoff Operational Knowledge and
complete its Git tag, GitHub Release, issue, Wiki, Discord, role-learning, and final-report gates in the same Publisher
task. Do not enter signoff while a platform result is failed, partial, or uncertain unless the user explicitly directs
an exceptional action.

## Stop on uncertainty

Stop and ask if any of these are unclear or inconsistent:

- requested mod name,
- requested version,
- package source path,
- final package contents,
- `version-X.X` folders,
- manifest versions,
- DLL/XML names,
- platform description synchronization,
- Steam `publishedfileid`,
- Mod.IO game/mod IDs,
- Steam or Mod.IO credentials.

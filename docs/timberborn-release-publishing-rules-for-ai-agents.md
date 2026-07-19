# Timberborn Release Publishing Rules for AI Agents

Use these rules when preparing, validating, or publishing Timberborn mod releases to Steam Workshop or Mod.IO.

Publishing is a high-risk workflow. Prefer stopping and asking over guessing.

For clean-checkout local environment requirements such as `_MODS!`, `_GAME!`, dependency links, Unity, SteamCMD, Mod.IO
tokens, GitHub CLI auth, generated references, and local release artifacts, follow
`docs/timberborn-new-repository-bootstrap-for-ai-agents.md`.

## Explicit publish requests only

Never publish to Steam or Mod.IO unless the user explicitly asks to publish.

Dry runs may build packages, validate files, generate metadata, and prepare staging directories. They must not upload
anything.

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

Issue closing and Wiki handoff remain explicit user-confirmed follow-up steps, not automatic side effects of the
publish script.

This unified tooling is still new. Until it has been proven by at least one successful real release publish flow, treat
unexpected script behavior as a stop-and-investigate condition instead of silently falling back to ad hoc manual steps.

## First release of a new mod

For a mod without a verified Steam `PublishedFileId` or Mod.IO `ModId`, use a one-time platform identity bootstrap and
then return to the ordinary release workflow. Do not maintain a separate first-release publishing pipeline.

1. **Prepare locally without platform mutation.** Confirm the exact mod identity, first version, compatibility lanes,
   title, package source and DLL, changelog, descriptions, thumbnail, category and compatibility metadata, and required
   dependencies. Create the ordinary release inputs, build and validate the final package, date the changelog, and
   commit the release preparation. A report may identify missing platform IDs, but it is preliminary and must not be
   marked `ReadyForPublish`.
2. **Bootstrap platform identities with separate explicit authorization.** The authorization must name the mod, each
   platform to create, and the initial visibility or publication state. Through a bounded creation mode or a documented
   manual step, show the exact account, game, title or slug, state, tags, dependencies, description source, and thumbnail
   before creating only the requested Workshop item or Mod.IO page. Live-verify the returned identity, ownership, game
   scope, mod identity, and state before adopting it. Never guess, reuse, or copy another mod's platform ID. Identity
   creation does not authorize package upload, a later visibility change, a Git tag, or a GitHub Release.
3. **Re-enter the normal immutable pipeline.** Record verified IDs in tracked release configuration or ignored local
   platform configuration according to repository convention; never store credentials. Commit any tracked identity or
   metadata adoption, configure and verify structured dependencies and compatibility/category tags, then rerun the full
   final preflight. From that point use the normal upload order, live verification, partial-success stop gate, tagging,
   GitHub Release, and post-release workflow.

Prefer dry-run-first identity-bootstrap tooling that fails when creation mode is not explicit, an item already exists,
or identity fields are incomplete. It must create and verify identities only and must not continue silently into package
upload.

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

## Same-version corrective package replacement

A same-version corrective replacement is an exceptional workflow for replacing already published package contents
without changing the mod version. Do not infer this mode from a packaging defect or an existing platform release. The
user must explicitly authorize replacement for the named mod, version, and platforms.

Prepare the correction as a committed package-layout or release-input change, then run a fresh final preflight for the
corrected artifact. Keep the existing release identity unchanged. The report must identify the correction commit,
corrected package hash and source fingerprint, requested replacement platforms, existing live artifact identities and
hashes when obtainable, and the chosen disposition of the existing pushed tag.

Use explicit corrective-replacement modes in unified and child platform tooling. Normal create-or-publish behavior must
continue to fail when the platform release or same-named asset already exists. If tooling cannot intentionally replace
an existing package or GitHub Release asset, stop and implement or validate that bounded mode; do not bypass the
workflow with ad hoc platform commands or an implicit clobber option.

Authorization to replace platform packages or assets does not authorize changing Git history. Do not create another
version tag or duplicate GitHub Release. Moving an existing pushed tag to the correction commit requires separate
explicit user authorization for that tag rewrite. If the user chooses to leave the tag at its original commit, record
that provenance difference explicitly before replacement and in the final report; do not imply that the tag contains
the corrected package-layout commit.

Before each replacement, re-check the unchanged mod and version identity plus the corrected artifact hash. Afterward,
verify that every requested platform exposes the corrected artifact or content and that its digest or closest available
content fingerprint matches the preflight snapshot. If replacement succeeds on only some platforms, stop, report the
partial public state, and require explicit direction before retrying or changing tag provenance.

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

When red-flag changes are present:

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

## Restoring an unchanged legacy compatibility lane

For a newly prepared `LocalModFolder` release, a required legacy compatibility lane may be restored before packaging
from a verified previously published artifact only when the release plan intentionally preserves that lane unchanged.
If it is unclear whether the new release behavior should also be rebuilt for the legacy game version, stop and ask
instead of carrying an older lane forward implicitly.

The release configuration must contain a lane-specific `ManifestVersions` value for the preserved lane. If it does
not, stop instead of inferring the expected legacy version from the artifact being considered.

Prepare, export, and build the new current lane through the normal workflow first. Never obtain the current lane from a
previous artifact. Restore a legacy lane only when it is missing or known stale; do not overwrite a present verified
lane merely because an older artifact is available.

Resolve legacy provenance in this order:

1. The verified previously published Steam Workshop item whose `PublishedFileId` matches the target mod's current
   release configuration.
2. An exact known local archive of that previously published release.

Do not use a backup folder, similarly named archive, neighboring Workshop item, or inferred previous output. Before
copying anything, verify:

- the platform or archive identity and the previously published mod version;
- the mod manifest identity;
- the exact legacy `version-X.X` lane name;
- the lane manifest version against its configured `ManifestVersions` value;
- the required DLL/XML files or an explicit configured legacy exception;
- any Unity bundles or other lane-owned content required by that published artifact.

Copy only the verified legacy lane into the exact configured `Package.SourcePath`. Record the Workshop item or archive,
published version, lane, and validation evidence in the preflight report or release handoff. After restoration, treat
the completed configured `LocalModFolder` as the only packaging input and run normal final-package validation across
all lanes.

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

## Post-release artifact comparison

When comparing an installed Steam Workshop copy with a local release source, first resolve the expected Steam
`PublishedFileId` from release config or repository tooling and verify that the installed Workshop folder manifest
belongs to the target mod. Do not infer the Workshop folder ID manually from memory, neighboring folders, or previous
investigations.

Distinguish the live release artifact from current `_MODS!` state. After unreleased Unity export or development changes,
`_MODS!/<ModName>` can be ahead of the last published release. For post-release artifact equality, compare against the
ZIP or staging folder produced by the actual release run, or verify before any later export/development changes. If only
current `_MODS!` is available after later changes, report it as current local development state, not as the published
artifact.

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

## Steam game-version coverage

Steam cannot target Timberborn Main and Experimental branches separately.

For Steam, if a package contains only one `version-X.X` folder, stop unless `Steam.AllowSingleGameVersion` is explicitly
configured with a `Steam.CompatibilityReason`.

Do not infer compatibility across major Timberborn versions.

## Steam visibility

Do not change Steam Workshop visibility during normal release publishing, description updates, metadata updates, or
package uploads.

Publication tooling must omit the Steam `visibility` field by default, even if local `workshop_data.json` contains a
`Visibility` value. A stored local visibility value is not permission to change the live Steam visibility.

Change visibility only when the user explicitly asks to change visibility for that specific mod and release operation.
In that case, use a command-level opt-in such as `-UpdateVisibility`, together with the requested visibility, and make
the dry-run output say that visibility will be updated.

If local data or release config already has `UpdateVisibility=true`, do not treat that as enough permission to change
the live Steam visibility. Stop unless the current user request explicitly asks for a visibility change and the
publication command includes the visibility-update opt-in.

If a normal publish dry-run shows that visibility will be updated, stop before upload and ask the user for explicit
confirmation. Treat accidental `Private`, `FriendsOnly`, or `Unlisted` visibility changes as release blockers.

## Platform tags

Treat the final `_MODS!/<ModName>/version-*` folders as the source of truth for platform compatibility tags.

Convert `version-X.Y` and `version-X.Y.Z` folders to platform update tags such as `Update X.Y`. Preserve non-version
tags from `workshop_data.json`, but replace version/update tags so they match the actual final package folders.

If a mod-local `AGENTS.md` documents a temporary package-layout exception where a single package folder intentionally
covers multiple Timberborn update tags, follow that local rule and stop if the generic tag plan would remove a required
compatibility tag. Keep such exceptions narrow and revisit them when the mod's compatibility model changes.

When a documented exception requires compatibility/update tags beyond the final package folders, express those tags in
the mod's `release.json` under `PlatformTags.AdditionalCompatibilityTags`. Values must be update tags such as
`Update 1.1`. The publish and tag-sync tools merge these explicit compatibility tags with folder-derived update tags
and still preserve non-version local tags from `workshop_data.json`.

Use `PlatformTags.AdditionalCompatibilityTags` only for compatibility/update tags that are intentionally not represented
by package folders. Do not use it for arbitrary category tags.

For Mod.IO, update tags through the Mod.IO tags API and verify the live tags after the update. Map local Steam-style tag
names to Mod.IO names where needed, such as `Quality of life` to `QoL` and `New content` to `New in-game content`.

For Steam, do not rely on SteamCMD `workshop_build_item` to update existing Workshop tags. SteamCMD may report success
while live tags remain unchanged. Use the repository Steam tag updater helper through `tools/update-platform-tags.ps1`,
then verify the live tags through Steam published-file details. SteamCMD remains the normal content upload path, but tag
synchronization is separate.

When publishing a Steam release, compute target tags from the final package's version folders before upload. If live
Steam tags differ, synchronize them before starting the SteamCMD upload and verify them after synchronization.

Do not change Steam visibility as part of tag synchronization.

## Mod.IO compatibility suffix

For Mod.IO, generate the game compatibility suffix from the actual `version-X.X` folders in the final package.

Example:

```text
---
MinimumGameVersion: 1.0.12.7
MaximumGameVersion: 1.1.99.99
---
```

Do not claim support for a game version unless the final package contains the corresponding `version-X.X` folder.

For Mod.IO uploads, prefer the target mod's own token file when it exists. In this repository, local Mod.IO token files
are equivalent owner tokens for the same account and game scope. If the per-mod token is missing but another known valid
owner token file exists, pass it explicitly with `-AccessTokenPath` while keeping the target mod's own Mod.IO config.
Do not ask solely because the token filename belongs to another mod. Do not copy, rename, or guess token files.

If the publish script stops only because the target mod's token file is missing, either create the target mod's local
token file when the user provides one, or rerun with an explicit `-AccessTokenPath` to another known owner token. After
upload, verify that the target mod's parent modfile ID and version point to the uploaded file.

After uploading a Mod.IO file, verify that the uploaded file becomes the live file. If Mod.IO reports the file as
uploaded but not live after scanning, explicitly activate the uploaded modfile through the Mod.IO API instead of
assuming upload success is enough.

## Platform descriptions

Local files under a mod's `Workshop` directory are the expected source for published platform descriptions.

Do not apply these rules to mods explicitly known as dead, unpublished, or kept only for reference. In this repository,
`TimberUI` is a dead mod kept only for reference; it does not build and must be excluded from release and platform
description synchronization checks.

Before publishing any mod, compare the local `Workshop` description files with the current descriptions published on
Steam Workshop and Mod.IO when platform access is available. This synchronization verification is mandatory for every
release.

Use `tools/verify-platform-descriptions.ps1` for this check when possible.

When verifying descriptions for a specific release, gate the release on the requested mod and platform targets. If a
shared verification command reports mismatches for unrelated mods, report them as background but do not block the
current mod release because of those unrelated mismatches.

If `tools/verify-platform-descriptions.ps1 -ModName <ModName>` returns nonzero while the requested mod's target
platform descriptions match, treat that as a tooling friction rather than a current-release blocker. Preserve the
distinction explicitly in the report: selected mod synchronized, unrelated mismatches reported separately. Do not ignore
nonzero exits when the requested mod or requested platform still has a mismatch.

If a local description and the published platform description differ, stop. Do not publish until the user decides which
side is correct:

- update the platform from the local `Workshop` file,
- update the local `Workshop` file from the platform,
- or manually reconcile the difference.

Do not silently overwrite either side when a mismatch is found.

Updating platform descriptions is not part of the normal release flow. Change local description files or published
platform descriptions only when the user explicitly asks for a description update.

Before publishing, review the target changelog section for serious player-facing capability changes. Ignore ordinary
fixes for this purpose. If the release adds new capabilities, removes capabilities, substantially changes what players
can do with the mod, or changes the mod's public positioning, propose updating the mod description on all published
platforms before release. Do not edit or publish description changes automatically; show the user what should be
updated and ask whether to prepare the new text.

When the user asks to update a description because release behavior changed, update it as the current product
description for a new player. Describe what the mod does now. Do not describe removed or historical behavior in the
main platform description. Put historical change information only in changelog or release notes when needed.

For a new description or a user-approved full description refresh, keep the player-value core focused on why a player
should install the mod, which gameplay problem it solves, its meaningful features, and how to use it. Do not fill that
core with implementation provenance, technical build details, routine expected faction support, game-version prose, or
dependency prose. State a faction limitation when it materially constrains use. Keep game compatibility and dependency
declarations in the required structured platform metadata and package manifest; their absence from promotional prose
does not weaken those publication checks.

When the repository-established Support or donation block is present, keep it at the top for visibility, followed by a
separator, the player-value core, another separator, and the source or community footer. Support and footer content is
allowed on the page but remains editorially separate from feature claims. This layout rule does not by itself authorize
restyling an existing description; preserve the existing structure unless the user requests and reviews the refresh.

Include a Discord link only when it leads to a dedicated channel for that mod, unless the user explicitly requests a
generic community invite. If no dedicated channel exists, omit the generic invite and add the direct channel link later
when it becomes available.

Preserve each platform's existing markup and file style. Steam Workshop descriptions use Steam formatting:

```text
https://steamcommunity.com/comment/Recommendation/formattinghelp
```

Example:

```text
[h1]Header[/h1]
[h2]Section[/h2]
[list]
[*]Item
[/list]
```

Mod.IO descriptions use regular HTML formatting, such as:

```html
<h1>Header</h1>
<h2>Section</h2>
<ul>
  <li>Item</li>
</ul>
```

When applying the same editorial change to both Steam and Mod.IO descriptions, keep the text semantically equivalent
but format it natively for each platform.

Use a small mechanical mapping where practical:

- Steam `[h1]` and `[h2]` map to Mod.IO `<h1>` and `<h2>`.
- Steam `[list]` with `[*]` items maps to Mod.IO `<ul>` with `<li>` items.
- Steam `[b]` maps to Mod.IO `<strong>`.
- Steam `[i]` maps to Mod.IO `<em>`.
- Steam `[url=...]text[/url]` maps to Mod.IO `<a href="...">text</a>`.

Preserve each platform's existing structure and make the smallest reviewed edit. Do not run an automatic
full-description conversion or restyle unless the user explicitly asks for it and reviews the result.

Make the smallest description edit that matches the current mod behavior. Do not regenerate or restyle the full
description unless the user explicitly asks for it.

Steam descriptions must be compared as exact text after normalizing CRLF/LF line endings only. Do not ignore trailing
spaces, blank lines, line breaks, punctuation, or other formatting characters in Steam descriptions. Steam formatting is
plain text markup, so whitespace may change rendering.

Mod.IO descriptions use HTML and the platform may normalize HTML attributes, links, entities, and whitespace. For
Mod.IO synchronization, compare the visible rendered text after HTML decoding and tag stripping instead of requiring
byte-for-byte HTML equality.

An existing Mod.IO token for one mod can be used to read descriptions for the other mods owned by the same account and
game scope. If the available tokens cannot read a mod description, stop and ask the user to create or provide a new
token.

When the user explicitly asks to update a Mod.IO description, prefer `tools/update-modio-description.ps1`. The script
does a dry run by default, updates only when `-Publish` is passed, supports explicit `-AccessTokenPath`, and verifies
the live Mod.IO visible HTML text after upload.

When the user explicitly asks to update a Steam description, prefer `tools/update-steam-description.ps1`. The script
does a dry run by default, updates only when `-Publish` is passed, and verifies the live Steam description after upload.
Do not hand-write Steam description VDFs unless the script is unavailable and the user explicitly approves the risk.
SteamCMD description VDFs are fragile: unescaped double quotes inside multiline `description` values can truncate the
published description. If a Steam description contains double quotes, replace them or improve and test the escaping
before publishing.

Keep Steam descriptions under the practical SteamCMD limit of about 8000 bytes. Do not silently shorten a Steam
description to fit this limit. If a description is too long, propose a shortened version for user review and publish it
only after the user approves the new text.

If SteamCMD parses the VDF but returns `Invalid Parameter` while updating a description, check description length first.
If shortening is needed, prepare a reviewed shorter description before retrying. Treat this limit as empirical and
replace it with an exact platform limit if tooling later proves one.

Avoid raw double quotes in local Steam description files unless the updater is proven to escape them safely. Prefer
single quotes or wording that avoids quotes instead of ad-hoc VDF escaping.

## Steam change notes

Steam change notes use Steam formatting.

Use:

```text
[h3]vX.Y.Z[/h3]
[*] Change item.
```

Convert changelog bullet lines from:

```text
* [Update] Something changed.
```

to:

```text
[*] [Update] Something changed.
```

## Steam login retry

Steam Guard mobile confirmation may take longer than an agent command timeout.

Use a login-only/retry mode that opens SteamCMD for interactive login and does not build, stage, or publish anything.

Do not store Steam passwords in repository files or local config files.

## Git release tags

After a mod release has been successfully uploaded and verified on all requested platforms, create a Git tag for the
published release commit.

Use the repository's existing mod-release tag pattern:

```text
<ModName>_<Version>
```

Examples:

- `Automation_4.7.0`
- `TimberCommons_1.19.0`

Do not add a `v` prefix for new mod-release tags unless the user explicitly asks.

The tag must point to the exact release-preparation commit recorded by final preflight for that mod. Never derive the
tag target from current `HEAD` at publish time. If `HEAD` contains later unrelated commits, create the tag against the
recorded commit after the preflight checks above succeed; do not make the earlier mod inherit the later commit. Verify
the recorded object ID still resolves to the expected commit and that the tag does not already exist before creating
it. The repository's existing mod-release tags are lightweight tags, so use a lightweight tag unless the user
explicitly asks for an annotated tag.

Changing an existing pushed tag to repair an incorrect target rewrites public release history. Do not delete, move, or
force-push that tag without explicit user authorization for the specific correction.

Create the tag only after platform uploads and live verification succeed. If publishing fails or is only partially
completed, do not create a release tag unless the user explicitly asks.

When Git remote access is available, push the tag after creating it. Report the tag name and whether it was pushed in
the final release summary.

## GitHub releases

After the release tag is created and pushed, create a GitHub Release for the same tag.

Use `tools/publish-github-release.ps1` for this step. The script dry-runs by default and publishes only with
`-Publish`.

The GitHub Release title should be `<display mod name> v<Version>`. The description should use the released version's
changelog section as-is, without Steam or Mod.IO formatting conversion. Attach the exact release ZIP that was published
to Steam Workshop and Mod.IO.

Do not create a GitHub Release before Steam Workshop and Mod.IO uploads and live verification succeed. If platform
publishing fails or is only partially completed, do not create a GitHub Release unless the user explicitly asks.

## Wiki handoff after release

After a successful mod release, consider whether the release changes any Wiki-facing surface:

- public API,
- mod component specs,
- configuration or data formats,
- user-facing behavior documented in the Wiki,
- compatibility notes,
- examples or scripting/reference material.

If the release plausibly affects the Wiki, send a delegated note to the `Wiki editor` thread. Include the mod name,
released version, release commit, relevant changelog bullets, and the Wiki areas that may need review.

Do not edit the Wiki directly as part of the publisher role unless the user explicitly expands the task.

Do not send a Wiki handoff for releases with no plausible Wiki impact. If uncertain, send a concise "may need review"
note instead of silently skipping.

## Publisher signoff handoff

At the end of a non-trivial publishing, release-preparation, platform-description, platform-tag, or post-release
investigation task, perform the root `AGENTS.md` role learning handoff check before signing off.

Do not wait for the user to ask for a mentor handoff. If the publisher task exposed a durable release, platform,
artifact, credential, staging, verification, or tooling lesson that is not already clearly covered by the rules, send a
concise delegated note to the mentor with the observation, evidence, suggested scope, and risk.

Do not send a mentor handoff only to say that an incident is understood or that an existing rule already covers it. In
that case, mention in the publisher's final report that no new mentor-rule update seems needed.

## Closing GitHub issues after release

When publishing a mod release, identify the issue-backed changes included in that release.

Before closing any GitHub issues, show the user the exact list of issues proposed for closure and ask for explicit
confirmation.

Never close GitHub issues automatically as a side effect of committing, pushing, packaging, or publishing.

If release scope is unclear, ask instead of closing issues.

When the user confirms closing GitHub issues after a release, use GitHub CLI when available:

```powershell
gh issue comment <issue-number> --repo ihsoft/TimberbornMods --body "Released in <ModName> vX.Y.Z."
gh issue close <issue-number> --repo ihsoft/TimberbornMods
gh issue view <issue-number> --repo ihsoft/TimberbornMods --json number,title,state,url,comments
```

Run authenticated `gh` commands outside the sandbox. In this environment, sandboxed `gh` may see an invalid or default
token because it runs without access to the user's keyring, while the user's normal `gh auth` works outside the
sandbox.

For each closed issue, add a release comment such as:

```text
Released in Automation v4.5.0.
```

After closing, verify the issue state with `gh issue view`.

If automatic issue closing is unavailable because GitHub CLI, tokens, browser access, or another authenticated path is
not available, do not treat the issues as closed. Give the user direct links to the confirmed issues and state clearly
that they still need manual closing.

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

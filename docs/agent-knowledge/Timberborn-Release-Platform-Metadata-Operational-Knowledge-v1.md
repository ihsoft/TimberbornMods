# Timberborn Release Platform Metadata Operational Knowledge

## Purpose

Use this document when changing or diagnosing Steam or Mod.IO compatibility tags, visibility, descriptions, change
notes, owner-only Private item verification, or Steam authentication. Also read the core release publishing rules.
Routine releases should use the compact verification gates in the core rules and load this document only when a plan
differs, metadata must change, or verification fails.

## Steam Game-Version Coverage

Steam cannot target Timberborn Main and Experimental branches separately.

For Steam, if a package contains only one `version-X.X` folder, stop unless `Steam.AllowSingleGameVersion` is explicitly
configured with a `Steam.CompatibilityReason`.

Do not infer compatibility across major Timberborn versions.

## Steam Visibility

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

## Platform Tags

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

For a publicly visible Steam item, when the authenticated owner query is unavailable, verified public details may be
used as read-only evidence that the live tags already match the exact target tags and no mutation is required. Do not
use this fallback to authorize tag changes, verify a Private or inaccessible item, or infer visibility. If the public
tags differ from the target, stop until owner-authenticated tag mutation is available. Keep `publish-steam.ps1` and
`update-platform-tags.ps1` consistent with this boundary.

When publishing a Steam release, compute target tags from the final package's version folders before upload. If live
Steam tags differ, synchronize them before starting the SteamCMD upload and verify them after synchronization.

Do not change Steam visibility as part of tag synchronization.

## Mod.IO Compatibility Suffix

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

For a first Mod.IO publication, keep the page Hidden through file upload, virus scanning, and active-file selection.
Only after the intended file is active and verified may the explicitly authorized publication step make the page
Public; then live-verify the page state. A Hidden identity page, even with an active file, is not a completed published
release.

## Platform Descriptions

For publishable mods, the mod's `release.json` is the tracked source for release-discoverable platform identity such as
`Steam.PublishedFileId`. Keep platform-description tooling driven by release configs and local platform configs instead
of hardcoded mod registries or copied Steam ID tables.

Local files under a mod's `Workshop` directory are the expected source for published platform descriptions. Use the
standard description paths:

```text
<ModName>/workshop/description.txt
<ModName>/workshop/description-ModIO.html
```

Do not add per-mod description path exceptions when a mod can instead be moved to the common file names.

Do not apply these rules to mods explicitly known as dead, unpublished, or kept only for reference.

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

For release-announcement handoff, the tracked Steam and Mod.IO descriptions are the source for the mod's direct
`discord.com/channels/<server>/<channel>` URL. Keep the direct URL consistent across both descriptions. Do not guess a
channel, choose between conflicting URLs, or substitute a generic invite. Report missing or conflicting metadata and
propose a reviewed description update when appropriate.

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

For a Private Steam item, a public `GetPublishedFileDetails` result indicating that the item is inaccessible is not
evidence that the item is absent. Prefer an authenticated owner Steamworks query for identity and description
verification.

If the owner query is temporarily unavailable and the user explicitly authorized the description update, the updater
may apply its reviewed generated VDF through an authenticated SteamCMD session. Keep Steam visibility omitted and
unchanged.

Treat SteamCMD `Committing update...Success` as acknowledgement that Steam accepted the update, not as verification of
the resulting description. Explicitly report the verification limitation and repeat owner-authenticated or public live
verification when an appropriate endpoint becomes available.

Keep this fallback inside the repository description updater. Do not normalize hand-written VDFs or ad hoc direct
SteamCMD commands as the ordinary workflow.

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

## Steam Change Notes

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

## Steam Login Retry

Steam Guard mobile confirmation may take longer than an agent command timeout.

Use a login-only/retry mode that opens SteamCMD for interactive login and does not build, stage, or publish anything.

Do not store Steam passwords in repository files or local config files.

Treat the Steam client Steamworks session and the SteamCMD cached login as separate sessions. If an authenticated owner
query reports `BLoggedOn=false` while SteamCMD still works, restart or reauthenticate the Steam client and repeat the
owner query; do not diagnose a missing item or platform failure from that state alone. Avoid unrelated SteamCMD work
between the final authenticated owner verification and the intended upload.

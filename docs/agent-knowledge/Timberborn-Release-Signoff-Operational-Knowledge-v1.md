# Timberborn Release Signoff Operational Knowledge

## Purpose

Load this document after every requested platform upload has completed and live verification has established a safe
signoff boundary, or when continuing post-release tag, GitHub Release, issue, Wiki, Discord, or final signoff work from
an already verified release. Also read the core release publishing rules. Do not create tags or GitHub Releases while
platform publication is failed, partial, or uncertain unless the user explicitly directs that exceptional action.

## Git Release Tags

After a mod release has been successfully uploaded and verified on all requested platforms, create the Git tags
recorded by final preflight for the changed source commits.

Use the repository's existing mod-release tag pattern:

```text
<ModName>_<Version>
```

Examples:

- `Automation_4.7.0`
- `TimberCommons_1.19.0`

Do not add a `v` prefix for new mod-release tags unless the user explicitly asks.

For an explicitly registered older compatibility line, use the qualified pattern
`<ModName>_<Version>_<ReleaseLine>`. When multiple lines changed in one package, create every recorded source tag and
push them atomically. The current-line unqualified tag is primary for the GitHub Release. When only an older line
changed, its qualified tag is primary. Follow the core release rules and Backport Operational Knowledge for the full
multi-source contract.

Each tag must point to its exact release-preparation commit recorded by final preflight. Never derive a tag target from
current `HEAD` at publish time or choose one commit to represent two changed worktrees. If `HEAD` contains later
unrelated commits, create the tag against the recorded commit after the preflight checks above succeed; do not make the
earlier mod inherit the later commit. Verify each recorded object ID still resolves to the expected commit and that no
tag already exists before creating it. The repository's existing mod-release tags are lightweight tags, so use
lightweight tags unless the user explicitly asks for annotated tags.

Changing an existing pushed tag to repair an incorrect target rewrites public release history. Do not delete, move, or
force-push that tag without explicit user authorization for the specific correction.

Create the tag only after platform uploads and live verification succeed. If publishing fails or is only partially
completed, do not create a release tag unless the user explicitly asks.

When Git remote access is available, push the tag after creating it. Report the tag name and whether it was pushed in
the final release summary.

## GitHub Releases

After the release tag is created and pushed, create a GitHub Release for the same tag.

Use `tools/publish-github-release.ps1` for this step. The script dry-runs by default and publishes only with
`-Publish`.

The GitHub Release title should be `<display mod name> v<primary SourceModVersion>`. When the primary source is an older
line, append ` (Timberborn v<TargetGameLine>)`; for example,
`X-Ray v1.1.2 (Timberborn v1.0)`. For a single-source release, the description uses that version's changelog section
as-is, without Steam or Mod.IO formatting conversion. For a multi-source release, include one clearly labelled section
per changed compatibility line, preserve each source changelog text, and line-qualify every older-source label. Attach
the exact release ZIP that was published to Steam Workshop and Mod.IO. The title's source version is independent of the
package-wide `PublicPackageVersion` used for the Mod.IO file.

Do not create a GitHub Release before Steam Workshop and Mod.IO uploads and live verification succeed. If platform
publishing fails or is only partially completed, do not create a GitHub Release unless the user explicitly asks.

## Discord Release Handoff

After successful publication, use `tools/prepare-discord-release-message.ps1` to prepare the mod's release announcement
from the display name and exact released-version changelog section. The unified publish tool invokes this handoff unless
Discord preparation was explicitly skipped, including when GitHub Release creation was skipped.

The announcement contains only the release title and exact changelog section. Do not append a download link, GitHub
Release link, or other acquisition instructions; subscribers in the dedicated mod channel already know where to obtain
the current version.

Format the release title as the neutral Markdown heading `# <display mod name> v<SourceModVersion>`, followed by a
blank line. For an older-line-only release, append ` (Timberborn v<TargetGameLine>)`. For a coordinated release whose
current line is primary, keep the current-line heading and identify every older source in its release-note section.
Render each top-level changelog item as a Markdown `-` bullet for Discord, even when the source changelog uses `*`.
Changing the bullet marker is formatting only; preserve the changelog item text exactly.

With `-Prepare`, the helper may open the tracked direct mod channel for the user, but must not copy the message to the
local clipboard. The user reviews, copies, and sends the message manually. Never automate a normal Discord user account,
send the message, or treat opening the channel as evidence that an announcement was posted.

Put the complete reviewed Discord message in a fenced `text` block at the very end of the Publisher's final release
response, after the release summary, verification, issue, and Wiki notes. Do not replace this visible handoff with a
clipboard-only delivery; the final block keeps the outstanding manual action durable and prominent.

Do not silently shorten, rewrite, or choose alternate player-facing text to fit Discord's 2000-character limit. If the
message is over the limit, or the direct channel URL is missing or conflicts between tracked descriptions, report the
problem and leave the announcement unsent. A Discord handoff failure after verified publication is a post-release
warning; it must not misreport the already completed release as failed.

## Wiki Handoff After Release

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

## Publisher Signoff Handoff

At the end of a non-trivial publishing, release-preparation, platform-description, platform-tag, or post-release
investigation task, perform the root `AGENTS.md` role learning handoff check before signing off.

Do not wait for the user to ask for a mentor handoff. If the publisher task exposed a durable release, platform,
artifact, credential, staging, verification, or tooling lesson that is not already clearly covered by the rules, send a
concise delegated note to the mentor with the observation, evidence, suggested scope, and risk.

Do not send a mentor handoff only to say that an incident is understood or that an existing rule already covers it. In
that case, mention in the publisher's final report that no new mentor-rule update seems needed.

## Closing GitHub Issues After Release

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

When the issue was delivered only by an older-line backport, line-qualify that comment, for example
`Released in X-Ray v1.1.2 (Timberborn v1.0).` For a coordinated release, identify each changed line relevant to the
issue instead of implying that one source version applies to every game line.

After closing, verify the issue state with `gh issue view`.

If automatic issue closing is unavailable because GitHub CLI, tokens, browser access, or another authenticated path is
not available, do not treat the issues as closed. Give the user direct links to the confirmed issues and state clearly
that they still need manual closing.

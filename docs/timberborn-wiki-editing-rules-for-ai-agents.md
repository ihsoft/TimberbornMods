# Timberborn Wiki Editing Rules for AI Agents

Use these rules when editing or reviewing the TimberbornMods GitHub Wiki.

The Wiki repository is separate from the main mod repository:

```text
Main repository: <repo-root>
Wiki repository: <repo-root>.wiki
Wiki branch: master
```

The Wiki checkout is a separate Git repository, not a directory inside the main repository. The expected Git URL is:

```text
https://github.com/ihsoft/TimberbornMods.wiki.git
```

If the sibling `<repo-root>.wiki` checkout is missing, ask the user whether to clone it there, locate an existing Wiki
checkout, or continue without Wiki edits. Explain that Wiki commits must be made in the separate Wiki repository. Do
not create Wiki pages inside the main repository.

Do not mix main-repository commits and Wiki commits.

## Source of truth

For Wiki content, the source of truth is the current `<repo-root>` repository state, not the current Wiki text,
changelog entries, release notes, or suggestions from other agents.

Before editing a Wiki page, verify public behavior against the mod code, package data, localizations, blueprints, or
other relevant repository files.

Treat suggestions from other agents as editorial input, not as text to accept automatically. The Wiki editor may reject,
narrow, reorganize, rewrite, or move suggested content when that makes the Wiki clearer or more accurate.

## Scope and audience

The Wiki should explain public behavior, public APIs, and useful workflows. It should not become a complete reference
for the Timberborn game internals.

For sections such as useful components, properties, or templates, choose a short curated list:

- items with high practical value for scripts, templates, or modder workflows,
- items that fill a gap where there is no dedicated signal or simpler API,
- items whose value can be explained clearly to the target reader.

Exclude technical, object-valued, enum, or low-level properties when the documented access operator or public workflow
does not support them, or when they would turn the page into a generic game reference.

## Scripting and API reference pages

For scripting/API reference pages, document what the scripting layer actually supports, including advanced or
manual-only capabilities that do not appear in the ordinary visual constructor UI.

When UI support is limited, state the limitation explicitly. For example, note when a feature can be used manually in a
script but is not exposed by the visual constructor.

When documenting access operators, remind readers that properties do not trigger rules by themselves. They must be used
with signals or another trigger source.

## Localization workflow

For localized Wiki pages, first settle the English or source-facing page structure and content.

Then synchronize localized pages from that source structure.

If a localized page is badly outdated, it is acceptable to update it as a larger synchronized block instead of trying to
mirror a small English diff line by line.

For release-driven Wiki updates, check page-level metadata, headers, or visible "last updated for version/date" stamps
on every touched source and localized page. When the page content is being brought current for a specific release,
update those stamps to the verified source release version and date. Leave them unchanged only when the edit is not a
version refresh.

## Markdown and rendering

Prefer ordinary GitHub Markdown when it is sufficient.

For images, prefer:

```md
![Alt text](url)
```

over raw HTML such as:

```html
<img src="url">
```

unless raw HTML is required for a specific layout that Markdown cannot express.

## Verification

Before committing Wiki changes, run whitespace validation in the Wiki repository:

```powershell
git diff --check
```

Line-ending warnings may be reported separately when the whitespace check itself is clean.

Before committing release-driven Wiki changes, also scan the top metadata/header block of each touched source and
localized page for stale version or date claims.

# Wiki Operational Knowledge

## Purpose

Provide operational knowledge for maintaining a Git-based project Wiki.

This document defines repository mechanics, page operations, navigation, link integrity, verification, and publication
gates. Editorial decisions and repository-specific content policy belong to the applicable Wiki editing rules.
Timberborn-specific content knowledge belongs to Timberborn Domain Knowledge.

## Wiki repository model

A project Wiki may be stored as a separate Git repository from the main source repository. Treat it as an independent
working tree with its own clone URL, branch, remote state, history, files, commit workflow, and push permissions.

Do not assume that editing or committing the main repository changes the Wiki.

## Safe working-tree preparation

Before editing:

1. Open the Wiki checkout specified by the repository rules.
2. Inspect the working-tree status, configured remote, active branch, and upstream relationship.
3. Compare them with the repository-specific Wiki instructions.
4. Determine whether the remote contains newer work without overwriting local changes.

Do not blindly pull, rebase, reset, or switch branches. If the working tree is dirty, the branch has diverged, the
remote is unexpected, or synchronization would alter local work, stop and ask before choosing a reconciliation path.

## Page workflow

A typical Wiki page workflow is:

1. Locate the relevant Markdown page in the verified Wiki checkout.
2. Read the current page completely and identify related source, localized, and navigation pages.
3. Make the smallest content change that satisfies the assignment.
4. Review the page diff and rendered Markdown structure.
5. Validate affected links, navigation, metadata, and localized pages.
6. Confirm that no unrelated pages changed.
7. Commit with a focused message only when authorized.
8. Push only when explicitly authorized, then distinguish the local commit from the public Wiki state.

## Markdown pages and links

- Preserve existing file naming and link conventions.
- Preserve front matter, metadata, and intentional formatting.
- Avoid unrelated formatting churn.
- Before renaming or deleting a page, check inbound links and public navigation references.
- After moving or renaming content, verify relative links from both the changed page and its known callers.
- Check affected local and external links when practical.

## Navigation

Navigation may be controlled through sidebars, footers, indexes, landing pages, or repository-specific conventions.
Before changing it, inspect the existing Wiki structure, preserve established ordering and naming, and verify that new
or moved pages remain reachable through the intended navigation path.

## Localized pages

When localized Wiki pages exist, identify the canonical source language from the repository rules. Update and stabilize
the canonical page first, then synchronize localized pages while preserving locale-specific filenames, links, and
terminology. Do not silently invent uncertain translations.

## Verification and authorization

Before considering the work complete:

- inspect working-tree and staged status;
- review the full diff;
- run the repository-required whitespace or Markdown checks;
- verify page names, references, navigation, and affected links;
- confirm the intended repository and branch are active;
- confirm that no unrelated files or pages changed;
- distinguish uncommitted edits, local commits, pushed commits, and publicly visible Wiki state.

Do not rewrite Wiki history, delete or rename public pages casually, mix main-repository and Wiki commits, or combine
release publication with Wiki editing unless the user explicitly authorizes that scope.

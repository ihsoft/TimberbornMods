# Timberborn Release Identity Bootstrap Operational Knowledge

## Purpose

Use this document only when a release target does not yet have a verified Steam `PublishedFileId` or Mod.IO `ModId`.
Also read the core release publishing rules. Return to the ordinary immutable release workflow after platform identities
are verified and adopted.

## First Release Of A New Mod

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

If Steam `CreateItem` returns or exposes a new `PublishedFileId` but a later profile, preview, verification, or receipt
step fails, treat the identity creation as partial success. Do not call `CreateItem` again. Recover only through an
explicit existing-ID mode that cannot create another item. Before mutation, verify the owner, consumer app, Private
visibility, and that any already-populated title matches the expected title; after completing the profile update,
live-verify the exact title and state. Write the local creation receipt only after that recovery succeeds. If no
trustworthy ID is available or any identity check differs, stop for owner investigation instead of guessing or creating
another item.

When a Mod.IO creation plan declares dependencies, require `community_options` to include the Mod.IO
`ALLOW_DEPENDENCY` capability bit (`1024`) before creating the page. A dependency preview is planning evidence only.
After creation, add the declared dependencies through the supported API and verify them by readback before final
preflight may pass.

Before a Steam item becomes public, verify it through an authenticated owner Steamworks UGC query. A missing or failed
result from the public published-file details endpoint is not evidence that an owner-visible Private item is missing or
invalid. The owner query must verify the intended item identity, title, exact description, visibility, tags, content
state, and dependencies. After publication, also verify that the item is visible through the public endpoint.

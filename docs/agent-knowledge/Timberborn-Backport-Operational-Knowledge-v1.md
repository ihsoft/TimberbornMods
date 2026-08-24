# Timberborn Backport Operational Knowledge

## Purpose

Use this document only when the user explicitly asks to adapt current mod behavior to an older Timberborn game line.
A backport creates a new changed artifact for the older line. It is not ordinary forward compatibility work and is not
the release-recovery workflow that restores an unchanged previously published compatibility lane.

Repository C#, modding, Unity, localization, diagnostics, real-game validation, package-validation, and release rules
still apply according to the files changed and operations performed.

## Explicit Opt-In

Never implement, build, package, or publish a backport automatically. At signoff for a current-line player-visible
change, when the mod has a documented older supported line and the change plausibly applies there, mention the backport
option once. That reminder does not authorize edits or generated artifacts.

An implementation request must identify the target mod and older game line. Clarify the intended source scope when it
is not explicit. A request to backport everything since a game-version transition authorizes an inventory of that
interval, not blind replay of every historical commit.

## Establish The Target And Baseline

Before editing:

1. Record the exact target Timberborn version, compatibility lane, and user-selected older game installation or local
   alias. Treat the local path as task environment, not a portable repository path.
2. Identify the latest published mod version, artifact, tag or commit, and package contents for that older line. Do not
   infer the baseline from the current lane or a neighboring archive.
3. Identify the current behavior or bounded set of source changes to reproduce. For an interval request, review both
   changelog history and Git history, then port the final surviving behavior rather than transient implementations that
   were later removed or replaced.
4. Verify that decompiled sources and extracted assets used as evidence have provenance for the target older game
   build. Current-game generated references are not evidence for the older API or data model.
5. Establish the code and package ownership model for the older line before implementation. Use an existing documented
   branch, worktree, project, lane, or conditional-build strategy when one exists. If none exists, propose the smallest
   maintainable boundary and ask before introducing a new long-lived support architecture.

For a registered compatibility worktree, treat its branch and worktree root as the source boundary. Use current tracked
tooling with an explicit target root when historical branch scripts lack required checks; do not copy current scripts
into the older branch or let a tool silently resolve source paths from its own checkout.

Register a proven release owner in `tools/release-lines.json` by stable branch, release-line identifier, game lane,
version policy, and mod name. Do not store a machine-local worktree path there; discovery resolves the branch through
Git and stops when its worktree is absent.

Generated references remain local to that worktree. Its `_DecompiledGame/` and `_ExtractedGameAssets/` caches must carry
matching provenance for the older game. A missing cache is a regeneration requirement, not permission to read the
current line's cache. Generate through an explicit target root so the old game cannot replace or mix with current-line
output.

Do not silently repoint shared game references, overwrite the current generated package, or make the current line
depend on older game assemblies merely to obtain a successful legacy build.

## Divergence Gate

Compare the target and current lines at the boundaries used by the requested behavior: game and publicized APIs,
dependency versions, lifecycle, save or script contracts, Unity and UI assets, localization, package layout, and build
or export tooling.

Continue without another approval only while the adaptation remains bounded and the selected ownership model remains
valid. Stop, summarize the discovered divergence, estimate the added maintenance surface, and ask whether to continue
when the backport requires substantial new or duplicated code, a different architecture, broad compatibility shims,
many rewritten assets, or a materially larger scope than the user authorized. Do not invent a fixed line-count
threshold; the decision is about maintenance and reversibility.

## Implementation Contract

Backport player-visible behavior, not source text mechanically. Prefer the older game's established APIs and patterns;
do not preserve current-line implementation details that exist only because the newer game requires them. Keep the
current line behavior and package output unchanged unless the user explicitly authorizes a shared refactor.

Maintain an explicit inventory of requested features and fixes with one of these outcomes for each item:

- implemented and validated on the older line;
- intentionally adapted with a documented behavior difference;
- omitted with user approval;
- blocked by a named game-version or architecture constraint.

Do not silently drop difficult items from an "all changes since" request. If one item triggers the divergence gate,
pause before expanding the implementation and report the remaining inventory.

## Version And Changelog Contract

Backport releases advance only the patch component of the latest published minor version on the target older line.
Features and fixes both use that next patch version. Do not apply the ordinary feature-to-next-minor rule to an
explicit older-line backport, and do not change the current line's version merely to make the numbers globally
monotonic.

Describe the net player-visible delta from the latest published version of the target older line. Keep normal
`[Feature]`, `[Fix]`, or other established entry categories; patch-only numbering is a line-management exception, not a
reason to misclassify the delivered behavior.

Give every new backport changelog section a line-qualified identity. Use
`# v<SourceModVersion> (Timberborn v<TargetGameLine>) (TBD)` while it is pending and preserve the Timberborn qualifier
when replacing `(TBD)` with the concrete release date. For example, X-Ray 1.1.2 for the Update 1.0 line is identified as
`X-Ray v1.1.2 (Timberborn v1.0)` in player-facing release titles. Do not silently rewrite an already dated historical
heading; correcting published history requires explicit user direction.

Before editing a shared changelog or release configuration, verify that current tooling can represent the target older
line without losing or misidentifying a current-line `(TBD)` section. If simultaneous pending lines conflict with a
top-section assumption, stop and establish a deliberate per-mod representation or tooling change instead of reordering
history or overwriting another line's pending release.

When the older line lives in its own worktree, its changelog is authoritative for that line. Candidate discovery and
version validation must inspect that worktree directly. The next backport version is the next patch after that
changelog's latest dated release, even when the current line has a higher minor version.

Ordinary implementation does not authorize release metadata changes. Leave lane-specific `release.json`, manifest,
package version, and platform metadata updates to explicit release preparation unless the user included them in the
task.

## Build And Validation

Build against the target older game assemblies and refresh every owner of the target compatibility lane. Keep legacy
and current generated package outputs distinguishable, and verify the old game loaded the intended local package and
lane from its current log before treating gameplay as evidence.

The user performs real-game validation in the target older Timberborn version. After confirmation, add or update
focused regression tests for the adapted behavior, run the target-line production build and package validation, and
complete the repository C# finalization gate. Do not use a successful current-line build or current-game test as proof
that the backport works.

After the first committed backport for a mod establishes stable production, test, export, and package ownership, update
the closest mod-local instructions and Package Build And Validation Matrix. Do not register a speculative path before
the first real committed candidate proves it.

## Release Boundary

Backport implementation and validation do not authorize publication. A Publisher task must still prepare a committed
release snapshot, run immutable preflight, publish only on explicit request, and verify all requested platforms.

A rebuilt backport lane is changed content. Never source it from the unchanged-legacy-lane restoration workflow in
Release Recovery Operational Knowledge. Build it from the verified older-line source and target toolchain. Preserve or
restore any other unchanged compatibility lane only through its own verified provenance workflow, and validate the
completed multi-lane package before publication.

Use a line-qualified Git tag for a backport source commit: `<ModName>_<Version>_<ReleaseLine>`, for example
`XRay_1.1.2_update-1.0`. The ordinary unqualified tag remains reserved for the current line. One Git tag cannot identify
two commits.

Keep two version concepts separate throughout preflight, publication, and signoff:

- `SourceModVersion` is the version of one changed source line. It owns that source's changelog section, manifest,
  immutable source record, and Git tag. For an older source, its release identity also includes the target Timberborn
  line.
- `PublicPackageVersion` is the current-line manifest version present in the final multi-lane package. It owns the
  package-wide version field on platforms that expose one, including the Mod.IO file version. When only an older line
  changes, this value remains the unchanged current-line version; do not replace it with `SourceModVersion` or present
  the combined package as a downgrade.

Discord headings, GitHub Release titles, Wiki handoffs, and signoff summaries that identify an older-line source must
use the line-qualified form `<display mod name> v<SourceModVersion> (Timberborn v<TargetGameLine>)`. Steam Workshop
change-note headings and release-note section labels may omit the repeated mod name, but must retain
`v<SourceModVersion> (Timberborn v<TargetGameLine>)`. Mod.IO change notes must name the same changed source and game
line even though the Mod.IO file version is `PublicPackageVersion`.

If current and older lines both change for one public package, create one source record and one tag per changed line.
The current-line source is primary: its ordinary tag owns the GitHub Release and public release title; the qualified
older-line tag points to the older worktree commit. Build and verify each changed lane independently, import the older
lane only from its immutable preflight package, and require the final combined preflight to match each lane fingerprint
and both source commits before any upload. `PublicPackageVersion` is the current source's `SourceModVersion`. Include
both changelog sections in release notes and handoffs, and line-qualify every older-source section.

If only the older line changes, its qualified tag is the primary GitHub Release tag. Preserve any unchanged current lane
only through verified published-artifact provenance; do not create a new current-line tag or imply that current-line
code changed. The older source's `SourceModVersion` owns the GitHub Release title and other line-qualified release
identity, while the unchanged current lane owns `PublicPackageVersion`.

A new older-line-only package upload may therefore publish another Mod.IO file with the same visible
`PublicPackageVersion`. This is a normal backport package update, not a same-version corrective replacement, only when
immutable preflight proves all of the following:

- the release has a new line-qualified older-source identity and tag;
- the final current lane is byte-for-byte identical to verified provenance for the active published package;
- the changed older lane matches its new immutable source and differs from the previously published lane fingerprint;
- the active Mod.IO file already reports the recorded `PublicPackageVersion`;
- publication creates a distinct file ID, and final readback verifies that new file as active, scanned, unflagged, and
  still labelled with `PublicPackageVersion`.

Do not apply this exception to a changed current lane, an unqualified source, an unchanged package, or an arbitrary
same-version replacement. Those cases remain governed by the corrective-release rules.

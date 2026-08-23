# X-Ray Agent Notes

These notes apply only when working inside the X-Ray mod.

## Release Package Layout

X-Ray requires separate compatibility lanes because the current Update 1.1 build is not compatible with Timberborn
Update 1.0:

- `XRay/version-1.0` is owned by the common `update-1.0` compatibility branch and its registered worktree;
- `XRay/version-1.1` contains current builds and Unity exports for Update 1.1.

The first committed Update 1.0 backport baseline is commit `aa2a2d23ccce7b996beaa01ab2b81e6c9aacc0d1` on
`update-1.0`, based on the published `XRay_1.1.1` line. That branch uses the same relative `XRay/XRay.csproj`,
`XRay.Tests/XRay.Tests.csproj`, and `ModsUnityProject/Assets/Mods/XRay` owners, Timberborn 1.0 assemblies, the branch's
Unity project/toolchain, and `version-1.0` package output. Backport versions advance patch-only from that line's latest
dated release.

Restore an unchanged `version-1.0` lane only through the verified release-recovery workflow. For a changed Update 1.0
lane, build and preflight it in the `update-1.0` worktree, then import it into a combined package from that immutable
preflight artifact. Never rebuild or overwrite it with current Update 1.1 code. A backport still requires explicit user
authorization.

Preserve old ZIP archives as historical artifacts unless the user explicitly asks to remove them.

## Platform Tags

Derive platform compatibility tags from the two final package lanes. A package containing both `version-1.0` and
`version-1.1` should produce both `Update 1.0` and `Update 1.1`.

Use ordinary tags such as `XRay_1.4.0` for current-line commits and qualified tags such as
`XRay_1.1.2_update-1.0` for Update 1.0 commits. When both lines change in one public package, create both tags; the
current-line tag is primary for the GitHub Release. When only Update 1.0 changes, its qualified tag is primary and no
new current-line tag is created.

## Unity Export

Export current X-Ray Unity resources into `version-1.1`. Pass `-GameVersion version-1.1` when the repository tooling
requires an explicit lane.

Never export current Unity resources into the preserved `version-1.0` lane as part of ordinary Update 1.1 release
preparation. After export and legacy restoration, validate both lanes independently and verify that platform tags match
the final package folders.

For `update-1.0`, use that worktree's Unity project with its matching editor and game imports, export `version-1.0`
first, and then rebuild `XRay/XRay.csproj` into the same old-line package. Do not use the main worktree's generated
references or Update 1.1 package as validation evidence.

## Object Transparency

Apply building transparency only to the model roots owned by `BuildingModel`: `FinishedModel`, `UnfinishedModel`, and
`FinishedUncoveredModel`. Do not traverse the entire entity hierarchy, because it can capture unrelated dynamic
visualizers and per-entity materials.

Handle stockpile goods through the owning `GoodVisualization` component. Preserve and restore the active state of its
visualization object; do not locate it by child-object name or call `Clear()` as a visibility toggle. Exclude its
renderer from shared material traversal and caches.

When enabling X-Ray with saved object transparency, keep the object-material synchronization deferred until the next
input-processing frame. Applying it in the same frame as terrain, wire, and resource updates caused a user-confirmed
hitch on a large map. Change this scheduling only with new profiling and real-game evidence.

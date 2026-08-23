# X-Ray Agent Notes

These notes apply only when working inside the X-Ray mod on the `update-1.0` compatibility branch.

## Compatibility-Line Ownership

This worktree owns X-Ray's Timberborn Update 1.0 source and `version-1.0` package lane. The first committed backport
baseline is `aa2a2d23ccce7b996beaa01ab2b81e6c9aacc0d1`, based on the published `XRay_1.1.1` line.

Use the relative owners in this worktree:

- production: `XRay/XRay.csproj`;
- focused tests: `dotnet run --project XRay.Tests/XRay.Tests.csproj`;
- Unity data: `ModsUnityProject/Assets/Mods/XRay`;
- package output: `XRay/version-1.0` under the selected old-game local mod root.

Build against the configured Timberborn 1.0 installation and this worktree's Unity project/toolchain. Do not use the
main worktree's Update 1.1 assemblies, generated references, Unity export, or package as evidence for this line.

## Version And Release

Backport releases advance patch-only from the latest dated release in this branch's XRay changelog. Features do not
promote the older line to a new minor version. Use the qualified tag `<ModName>_<Version>_update-1.0`, for example
`XRay_1.1.2_update-1.0`; the unqualified tag is reserved for the current line.

Run current provenance-capable release and generated-reference tools from the tooling checkout with this worktree as
their explicit target root. A historical script in this branch that lacks current provenance or immutable-preflight
support is not an alternative release path.

A changed Update 1.0 lane must receive its own immutable preflight. Import it into a multi-lane public package only
through that preflight artifact and verify the lane fingerprint. When current and Update 1.0 lines both change, both
source commits receive tags and the current-line tag is primary for the GitHub Release. When only Update 1.0 changes,
the qualified tag is primary and no current-line tag is created.

## Unity Export

Export `version-1.0` through this worktree's Unity project first, then build `XRay/XRay.csproj` into the same package.
Verify the final package contains both the exported Unity layer and `Scripts/XRay.dll` plus `Scripts/XRay.xml` before
asking the user to test it in Timberborn 1.0.

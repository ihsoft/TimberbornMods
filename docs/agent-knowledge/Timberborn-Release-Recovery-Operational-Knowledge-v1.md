# Timberborn Release Recovery Operational Knowledge

## Purpose

Use this document only for a same-version corrective replacement, restoration of an intentionally unchanged legacy
compatibility lane, or comparison of a published artifact with local release state. Also read the core release
publishing rules. Ordinary releases should not load this document preemptively.

## Same-Version Corrective Package Replacement

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

## Restoring An Unchanged Legacy Compatibility Lane

For a newly prepared `LocalModFolder` release, a required legacy compatibility lane may be restored before packaging
from a verified previously published artifact only when the release plan intentionally preserves that lane unchanged.
If it is unclear whether the new release behavior should also be rebuilt for the legacy game version, stop and ask
instead of carrying an older lane forward implicitly.

This workflow never creates or updates a backport. When the user requests changed behavior for the older game line,
follow Timberborn Backport Operational Knowledge and build that lane from its verified source and target toolchain;
do not copy an older published artifact and then modify it in place.

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

## Post-Release Artifact Comparison

When comparing an installed Steam Workshop copy with a local release source, first resolve the expected Steam
`PublishedFileId` from release config or repository tooling and verify that the installed Workshop folder manifest
belongs to the target mod. Do not infer the Workshop folder ID manually from memory, neighboring folders, or previous
investigations.

Distinguish the live release artifact from current `_MODS!` state. After unreleased Unity export or development changes,
`_MODS!/<ModName>` can be ahead of the last published release. For post-release artifact equality, compare against the
ZIP or staging folder produced by the actual release run, or verify before any later export/development changes. If only
current `_MODS!` is available after later changes, report it as current local development state, not as the published
artifact.

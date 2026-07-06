# X-Ray Agent Notes

These notes apply only when working inside the X-Ray mod.

## Release Package Layout

X-Ray currently publishes as a single-folder cross-game-version package.

Do not create a `version-1.1` package folder unless the compatibility model changes. The current release package should
contain only `XRay/version-1.0`, even when publishing mod version `1.1.x` for Timberborn 1.1 compatibility.

Use release metadata compatibility ranges to express supported Timberborn versions. Do not infer that a mod version
`1.1.x` requires a `version-1.1` package lane.

Preserve old ZIP archives as historical artifacts unless the user explicitly asks to remove them.

## Platform Tags

Temporary current-model exception: while X-Ray uses the single `version-1.0` package folder with release metadata
declaring compatibility through Timberborn 1.1, keep both `Update 1.0` and `Update 1.1` platform tags.

Do not remove `Update 1.1` only because the final package has no `version-1.1` folder. If the generic platform-tag
tooling plans to remove `Update 1.1`, stop before publishing and ask for an override or tooling fix.

Re-evaluate this exception when Timberborn adds a new major/minor game-version lane or when X-Ray's package or
compatibility model changes.

## Unity Export

When exporting X-Ray Unity resources, pass `-GameVersion version-1.0` to the repository export tooling under the
current package layout. Do not let a generic export default create or refresh `version-1.1` unless the X-Ray
compatibility model changes.

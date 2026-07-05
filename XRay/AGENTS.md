# X-Ray Agent Notes

These notes apply only when working inside the X-Ray mod.

## Release Package Layout

X-Ray currently publishes as a single-folder cross-game-version package.

Do not create a `version-1.1` package folder unless the compatibility model changes. The current release package should
contain only `XRay/version-1.0`, even when publishing mod version `1.1.x` for Timberborn 1.1 compatibility.

Use release metadata compatibility ranges to express supported Timberborn versions. Do not infer that a mod version
`1.1.x` requires a `version-1.1` package lane.

Preserve old ZIP archives as historical artifacts unless the user explicitly asks to remove them.

## Unity Export

When exporting X-Ray Unity resources, pass `-GameVersion version-1.0` to the repository export tooling under the
current package layout. Do not let a generic export default create or refresh `version-1.1` unless the X-Ray
compatibility model changes.

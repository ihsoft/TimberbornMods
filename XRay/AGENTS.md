# X-Ray Agent Notes

These notes apply only when working inside the X-Ray mod.

## Release Package Layout

X-Ray requires separate compatibility lanes because the current Update 1.1 build is not compatible with Timberborn
Update 1.0:

- `XRay/version-1.0` preserves the verified last published artifact that remains compatible with Update 1.0;
- `XRay/version-1.1` contains current builds and Unity exports for Update 1.1.

Restore the unchanged `version-1.0` lane only through the verified legacy-lane workflow in the release publishing
rules. Do not rebuild or overwrite it with current Update 1.1 code. Prepare a new Update 1.0 build only when the user
explicitly asks for that separate compatibility task.

Preserve old ZIP archives as historical artifacts unless the user explicitly asks to remove them.

## Platform Tags

Derive platform compatibility tags from the two final package lanes. A package containing both `version-1.0` and
`version-1.1` should produce both `Update 1.0` and `Update 1.1`.

## Unity Export

Export current X-Ray Unity resources into `version-1.1`. Pass `-GameVersion version-1.1` when the repository tooling
requires an explicit lane.

Never export current Unity resources into the preserved `version-1.0` lane as part of ordinary Update 1.1 release
preparation. After export and legacy restoration, validate both lanes independently and verify that platform tags match
the final package folders.

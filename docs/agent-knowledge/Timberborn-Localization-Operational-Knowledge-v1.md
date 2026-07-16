# Timberborn Localization Operational Knowledge

## Purpose

Provide the repository workflow for adding or changing player-facing text, localization keys and files, translations,
localized UI strings, and `ILoc` usage.

This document does not replace:

- local `AGENTS.md` files or mod notes for package-specific paths, locale sets, and key prefixes;
- UI Toolkit notes for selecting `LocalizableButton`, `LocalizableLabel`, and other localizable controls;
- Unity Operational Knowledge and repository validation knowledge for export and real-game package preparation;
- the root real-game validation gate for player-visible behavior.

## Locate The Owning Files

Inspect the target package before editing. Localization files are CSV stored as `.txt`, usually under:

```text
<DataRoot>/Localizations/<locale>.txt
```

Do not assume that every mod has the same package path or locale set. Follow the closest local instructions and the
files that actually belong to the target package.

In this repository, use `enUS` as the canonical source locale unless closer local instructions explicitly define a
different source. For an existing package, add each new key to every locale file already present for that package.

For a new package, create `enUS` and the locales required by the task or package scope. Do not infer a repository-wide
required locale set from another mod.

## Translation Workflow

When adding or editing localized UI text, do not translate only from the English wording. First search the base game's
extracted localization files for the same feature, building, resource, status, action, or nearby concept. Prefer the
game's established terminology and style, adjusting capitalization and punctuation to the local UI context.

If no close game wording exists, translate normally and follow the target mod's existing localization style. When a
wording choice depends on a game term, cite or mention the exact game localization keys used as evidence in the final
response or implementation notes.

Do not invent an uncertain translation or silently omit a locale. If a reliable translation cannot be produced, name
the affected locale and ask the user for wording or explicit approval to use the `enUS` text as a fallback.

Preserve the useful player-facing meaning. Do not expose polling intervals, update buckets, or other internal mechanics
unless players need that information to make a decision. Put useful implementation context in the English localization
comment, code comments, or documentation instead.

For tooltips, do not remove important meaning merely to shorten the text. Use intentional line breaks inside a quoted
CSV field when a long single-line tooltip would render poorly in Timberborn UI.

## File Format

Each localization file has exactly these columns:

```csv
ID,Text,Comment
```

- `ID` is the stable localization key. Preserve existing IDs unless the task intentionally includes a key migration.
- `Text` is the localized player-facing text.
- `Comment` is always English in every locale file and should explain context rather than repeat the text.
- Preserve parameters such as `{0}`, `{1}`, and `{0:0.#}` with the same names, indexes, and format specifiers in every
  locale.
- Quote text and comment fields when they contain commas, parentheses, parameters, quotes, or line breaks. Escape a
  literal double quote according to CSV rules.
- Preserve the existing file encoding, line endings, row organization, and unrelated text.

Example:

```csv
Some.Mod.Feature.Action,"Do something ({0})","Button text. Explains what the action does."
```

Place new rows near related keys when the existing file is organized by feature. Do not reorder or reformat an entire
localization file as part of a narrow text change.

## Keys In Code And UI

Declare localization keys as constants near the top of the class that uses them:

```csharp
const string ActionLocKey = "Some.Mod.Feature.Action";
```

Resolve code-owned text through `ILoc.T(...)`:

```csharp
label.text = _loc.T(ActionLocKey, count);
```

Use the localizable Timberborn UI control appropriate to the element when the UI Toolkit notes define one. Do not
hardcode visible English strings in C#, UXML, or runtime UI construction.

When intentionally renaming a localization key, update every code, UXML, and locale-file reference. Search for the old
key before removing it and do not treat a key rename as ordinary wording cleanup.

## `ILoc` In Early Harmony Patches

Harmony patches are often static while Timberborn services are supplied through dependency injection. If a static
patch needs `ILoc`, use a small bridge and initialize it at the earliest verified safe lifecycle point.

Do not assume `ILoadableSingleton.Load()` runs before a UI-related patch. A patch may execute after the initializer is
constructed but before `Load()`. When that ordering is possible, initialize the bridge in the constructor:

```csharp
sealed class SomePatchInitializer : ILoadableSingleton {
  public SomePatchInitializer(ILoc loc) {
    SomePatch.SetLoc(loc);
  }

  public void Load() {
  }
}
```

Keep the static bridge narrow and follow the target mod's existing configurator and registration pattern.

## Validation

Before submitting a localization change:

1. Parse every touched localization file with a real CSV parser rather than validating it by line splitting.
2. Confirm the header and three-column structure remain intact.
3. Confirm every new or renamed key appears exactly once in every existing locale file for the package.
4. Confirm parameter sets and format specifiers match across locales.
5. Search the affected code and UI for accidental hardcoded player-facing text and stale key references.
6. Report pre-existing unrelated locale-parity problems instead of silently expanding the task to repair them.
7. Use the owning Unity export or package build path, then follow the root real-game validation gate for the rendered
   player-visible result.

If a locale remains incomplete by explicit user decision, report the exact locale and fallback behavior in the final
response.

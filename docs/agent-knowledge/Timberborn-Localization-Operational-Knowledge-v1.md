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

## UI Wording And Available Space

Most localized mod strings are displayed in UI where space is limited. For a button, toggle, tab, row label, or short
status, prefer the shortest wording that remains clear in the actual UI context.

Treat the surrounding UI as part of that context. A panel title, selected building, familiar icon, or single available
action may already make the target obvious. For example, `Stop` is usually better than `Stop building` on a building
panel when no other stopping action is nearby.

Use more explicit wording when nearby controls stop different things, the target or consequence is ambiguous, or the
action is dangerous or difficult to reverse. Clarity takes priority over brevity in those cases.

Tooltips, confirmations, errors, and explanatory text may be longer because their purpose is to supply context. Preserve
the information a player needs to decide or recover. Use intentional line breaks inside a quoted CSV field when a long
single-line tooltip would render poorly in Timberborn UI.

Judge available space and natural phrasing separately for every locale. A translation does not need to preserve the
English sentence structure when a shorter natural phrase conveys the same meaning, but do not remove important meaning
only to force the text into a smaller space.

## Translation Workflow

For every translated player-facing string, search the base game's extracted localization files for the same feature,
building, resource, status, action, or nearest useful concept. Compare the canonical and target-language game strings
for the same keys. Use the game's established terminology and style instead of inventing new terms, adjusting
capitalization and punctuation to the local UI context.

Keep this research bounded by the concrete wording decision. Stop once the relevant game term or the absence of a close
equivalent is established; do not search indefinitely for a perfect match.

If no close game wording exists, translate normally and follow the target mod's existing localization style. When a
wording choice depends on a game term, cite or mention the exact game localization keys used as evidence in the final
response or implementation notes.

Do not invent an uncertain translation or silently omit a locale. When material uncertainty remains, ask the user
before finalizing the text. Provide two or three viable options when possible, with:

- a literal back-translation into a language the user understands;
- the important tone or meaning difference;
- the expected UI-length tradeoff;
- matching game terminology or the absence of a close game term;
- a recommendation and confidence level.

Provide this assessment even when the user does not understand the target language. If no reliable translated option
can be selected, name the affected locale and ask for explicit approval before using the `enUS` text as a fallback.

Preserve the useful player-facing meaning. Do not expose polling intervals, update buckets, or other internal mechanics
unless players need that information to make a decision. Put useful implementation context in the English localization
comment, code comments, or documentation instead.

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

1. Parse every touched localization file with a real CSV parser rather than validating it by line splitting. If the
   package has any locale besides `enUS`, parse every localization file for the package.
2. Confirm the header and three-column structure remain intact, every `ID` is unique within its file, and empty `Text`
   fields are reported.
3. Compare the complete key set of every non-canonical locale with the canonical source locale, normally `enUS`.
   Report every missing and extra key, including the affected locale and exact key names.
4. Confirm parameter sets and format specifiers match across locales for every shared key.
5. Distinguish problems introduced by the current change from pre-existing localization debt when repository history or
   the current diff provides that evidence.
6. Do not automatically repair pre-existing missing keys, extra keys, duplicates, placeholder mismatches, or uncertain
   translations outside the requested scope. Report them and wait for explicit direction.
7. Search the affected code and UI for accidental hardcoded player-facing text and stale key references.
8. Use the owning Unity export or package build path, then follow the root real-game validation gate for the rendered
   player-visible result.

If a locale remains incomplete by explicit user decision, report the exact locale, missing keys, and fallback behavior
in the final response.

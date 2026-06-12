# Timberborn Modding Rules for AI Agents

This document defines general Timberborn modding rules and practical knowledge for AI agents working on this repository.

Use this together with:

- `csharp-formatting-rules-for-ai-agents.md`

The C# formatting file controls code style.
This file controls Timberborn-specific modding practices that are broadly useful across mods.

## Repository context

The main repository is:

```text
https://github.com/ihsoft/TimberbornMods
```

The repository contains multiple Timberborn mods.

Depending on the mod type, the mod package files may live in different places.

Common locations:

```text
Mod/
ModsUnityProject/Assets/Mods/<ModName>/Data/
```

Do not assume there is only one `Mod` directory.
For Unity-based mods, inspect the project files and existing asset references to find the actual package data location.

## Localization

Timberborn localization files are CSV files stored as `.txt`.

They are usually located under:

```text
<DataRoot>/Localizations/<language>.txt
```

Example:

```text
<DataRoot>/Localizations/enUS.txt
<DataRoot>/Localizations/ruRU.txt
```

There may be more languages, such as:

```text
deDE.txt
frFR.txt
```

When adding new localization keys, update all existing localization files when practical.

## Localization file format

Localization files have three columns:

```csv
ID,Text,Comment
```

Rules:

- `ID` is the localization key.
- `Text` is the localized user-facing text.
- `Comment` is always in English, in every language file.
- Use comments to explain context, not just repeat the text.
- Preserve parameters like `{0}`, `{1}`, `{0:0.#}` across all languages.
- Quote text/comment fields when they contain commas, parentheses, or parameters.

Example:

```csv
Some.Mod.Feature.Action,"Do something ({0})","Button text. Explains what the action does."
```

## Localization keys in code

Declare localization keys as constants near the top of the class that uses them.

Preferred pattern:

```csharp
const string ActionLocKey = "Some.Mod.Feature.Action";
```

Use `ILoc.T(...)` for localized text.

Example:

```csharp
label.text = _loc.T(ActionLocKey, count);
```

## `ILoc` and Harmony patches

Harmony patches are often static.
Timberborn services are usually provided through DI.

If a static Harmony patch needs a DI service such as `ILoc`, use a small bridge:

```csharp
static ILoc _loc = null!;

public static void SetLoc(ILoc loc) {
  _loc = loc;
}
```

Then create an `ILoadableSingleton` that receives the dependency from DI.

Important lifecycle rule:

Do not assume `ILoadableSingleton.Load()` runs before UI-related Harmony patches.

A patch can execute after the singleton object is constructed but before `Load()` is called.

Therefore, if the patch may run early, initialize the static bridge in the singleton constructor, not in `Load()`.

Preferred:

```csharp
sealed class SomePatchInitializer : ILoadableSingleton {
  public SomePatchInitializer(ILoc loc) {
    SomePatch.SetLoc(loc);
  }

  public void Load() {
  }
}
```

Avoid relying on this:

```csharp
sealed class SomePatchInitializer : ILoadableSingleton {
  readonly ILoc _loc;

  public SomePatchInitializer(ILoc loc) {
    _loc = loc;
  }

  public void Load() {
    SomePatch.SetLoc(_loc);
  }
}
```

Reason:

For UI patches, `Load()` may be too late.

## Harmony patching practices

Prefer small, targeted patches.

For UI patches:

- Prefer postfix patches when possible.
- Avoid replacing original behavior unless necessary.
- Use existing UI elements as anchors.
- Avoid depending on UXML when a safe runtime insertion is enough.
- Use `root.Q<T>("ElementName")` to find stable named elements.
- Gracefully return if an expected element is missing.
- Avoid duplicate insertion by checking for the new element name first.

Example:

```csharp
var targetButton = root.Q<Button>("TargetButton");
if (targetButton?.parent == null) {
  return;
}

if (root.Q<Button>("NewButton") != null) {
  return;
}
```

## UI manipulation

For UI Toolkit elements, hiding existing elements with `DisplayStyle.None` is a safe first approach.

Example:

```csharp
element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
```

This is often safer than rebuilding lists or modifying source collections.

## Accessing private fields

When needed, use Harmony `AccessTools`.

Example:

```csharp
static SomeType GetSomePrivateField(SomeClass instance) {
  return AccessTools.FieldRefAccess<SomeClass, SomeType>(instance, "_somePrivateField");
}
```

Use this sparingly.
Prefer public APIs when they exist.

## Registration

When adding helper singletons, register them using the existing mod configurator pattern.

Search the repository for existing registrations of:

```csharp
ILoadableSingleton
```

and follow the local convention.

Do not invent a new DI pattern if the mod already has one.

## General guidance

When working on Timberborn mods:

- Inspect decompiled game code when the behavior depends on game internals.
- Ask for or use ILSpy snippets for relevant game classes.
- Prefer minimal patches against stable-looking methods/classes.
- Keep compatibility risk low.
- Avoid assumptions about game load order unless tested.
- Preserve existing mod architecture and naming conventions.
- Keep localization keys close to the class that uses them.
- Update all existing localization files when adding user-facing text.

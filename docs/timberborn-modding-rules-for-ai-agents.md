# Timberborn Modding Rules for AI Agents

This document defines general Timberborn modding rules and practical knowledge for AI agents working on this repository.

Use this together with:

- `csharp-formatting-rules-for-ai-agents.md`

The C# formatting file controls code style.
This file controls Timberborn-specific modding practices that are broadly useful across mods.

## Ask when intent is unclear

Do not guess project intent.

Code shows implementation.
It does not always show design intent.

When multiple architectural choices seem reasonable, ask.

Examples:

- Should this functionality be generic or feature-specific?
- Should this code be reused elsewhere?
- Is this intended to become infrastructure?
- Is backward compatibility important?
- Is there an existing design decision behind the current implementation?

A short question is usually preferable to making the wrong architectural decision.

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

When adding or editing localized UI strings, do not translate only from the English source text.

First search the base game's extracted localization files for the same feature, building, resource, status, action, or
nearby concept. Prefer the game's established terminology and style, adjusting capitalization and punctuation to the
local UI context.

If no close game wording exists, translate normally and keep the existing mod localization style.

When the wording choice depends on a game term, cite or mention the exact game localization keys used as evidence in
the final response or implementation notes.

Do not expose implementation frequency, polling intervals, update buckets, or other internal mechanics in
player-visible names unless players need that detail to make a decision. Put such details in localization comments,
code comments, or documentation when useful.

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

When patching Timberborn game methods, prefer `nameof(TargetType.Method)` over string method names whenever the
publicized game assemblies make the target method compile-visible. This lets compilation catch renamed or removed game
methods after a Timberborn update.

If publicized direct access is not available for the target method, use the existing reflection or `AccessTools`
approach instead of forcing `nameof`.

When patching multiple methods on the same target type, prefer one patch class annotated with
`[HarmonyPatch(typeof(TargetType))]` and method-level `[HarmonyPatch(nameof(TargetType.Method))]` attributes, unless a
different shape is required by the target signature or patch discovery.

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

When adding or changing in-game UI assets, first find the closest existing game or mod UI element with the same role.
Reuse its UI Toolkit component type and classes before hand-styling controls. For example, prefer the same
`NineSliceTextField`, button classes, validation classes, and template structure used by nearby dialogs over recreating
input backgrounds and padding manually.

## Accessing private fields and publicizer

Before using Harmony `AccessTools` for private/internal game members, inspect the mod project file.

Some mods use `BepInEx.AssemblyPublicizer.MSBuild` and publicized game references.

Example project-file pattern:

```xml
<PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.2" PrivateAssets="all" />
<Reference Include="..\Dependencies\GameRoot\Timberborn_Data\Managed\Timberborn.*.dll" Publicize="true" />
<Reference Include="..\Dependencies\GameRoot\Timberborn_Data\Managed\UnityEngine.UIElementsModule.dll" Publicize="true" />
```

If the target assembly is publicized, prefer direct member access.

Important:

The target assembly may be publicized either by an explicit reference or by a wildcard reference pattern.

Example explicit reference:

```xml
<Reference Include="..\Dependencies\GameRoot\Timberborn_Data\Managed\Timberborn.ModdingUI.dll" Publicize="true" />
```

Example wildcard reference:

```xml
<Reference Include="..\Dependencies\GameRoot\Timberborn_Data\Managed\Timberborn.*.dll" Publicize="true" />
```

In the wildcard example, assemblies such as `Timberborn.ModdingUI.dll` are publicized even if they are not listed explicitly.

When checking whether direct access is available, match the target assembly against all `Reference Include` entries, including wildcard patterns, and use the effective `Publicize` value for the matching reference.

Preferred when publicizer applies:

```csharp
var value = instance._somePrivateField;
```

Avoid unnecessary `AccessTools` in that case.

If publicizer is not available for the target assembly, use Harmony `AccessTools` when needed.

Example:

```csharp
static SomeType GetSomePrivateField(SomeClass instance) {
  return AccessTools.FieldRefAccess<SomeClass, SomeType>(instance, "_somePrivateField");
}
```

Performance rule:

- `AccessTools.FieldRefAccess` is relatively expensive.
- A single call in initialization or low-frequency UI code is usually acceptable.
- Do not call it repeatedly in hot paths.
- If repeated access is needed and direct publicized access is unavailable, cache the result/delegate or move the lookup out of the hot path.

Prefer public APIs first, publicized direct access second, and `AccessTools` only when needed.

## Configurators and DI registration

Each package should have its own configurator.

A configurator implements `IConfigurator` and is marked with one or more `Context` attributes.

Known Timberborn contexts:

```text
Bootstrapper
MainMenu
Game
MapEditor
```

Choose the context from the task:

- `Game` for gameplay, in-game UI, entity components, services, ticks, save/load, tools, and patches used in a loaded
  game.
- `MainMenu` for main menu systems, mod settings, and mod-manager-related UI.
- `MapEditor` for systems that must run in the map editor.
- `Bootstrapper` is not for ordinary mod features. Use it only when there is clear evidence that the system must run
  during bootstrap.

A configurator can be registered in more than one context when the same bindings are required in multiple places.

Any type defined by a package that must participate in DI must be registered by the package configurator.

Common binding patterns:

```csharp
containerDefinition.Bind<SomeService>().AsSingleton();
containerDefinition.Bind<SomeComponent>().AsTransient();
```

Use `AsSingleton()` for shared services, settings, managers, UI modules/fragments, and objects expected to have one
instance per context.

Use `AsTransient()` for entity components, UI rows/dialogs created repeatedly, short-lived helpers, and objects that
must be constructed per owner or per request.

When adding helper singletons, register them using the existing mod configurator pattern.

Search the repository for existing registrations of:

```csharp
ILoadableSingleton
```

and follow the local convention.

### Game service bindings and contexts

When using a Timberborn game service, first find all DI bindings for that service and inspect their `Context`
attributes.

Do not assume that a service has the same implementation in `Game`, `MapEditor`, `MainMenu`, and `Bootstrapper`.
Some services are real in one context and dummy/no-op in another.

Before changing or replacing a game service binding:

- Find every `Bind<T>()` for the service.
- Identify the implementation used in each context.
- Inspect all consumers in the affected context.
- Check whether the service is intentionally disabled by a dummy implementation.

If a service is bound to a dummy/no-op implementation in `Game`, do not replace it globally unless all consumers in
that context have been reviewed. Prefer a narrow mod-owned service or scoped wrapper when the mod only needs a small
part of the behavior.

When implementing event-capture behavior, verify the event order in decompiled game code. For example, entity creation
events may be posted before the entity has finished initialization, so state that depends on initialized components may
need to be captured later.

When state ownership and event order matter, prefer observing the method that owns the state transition after it has
run instead of subscribing to a lower-level event that may fire before the owner updates its state. A small targeted
postfix can be safer than an earlier event subscription when the mod needs the final post-update state.

### Template decorators

Some entity components are added through Timberborn template decorators.

Typical pattern:

```csharp
containerDefinition.Bind<SomeComponent>().AsTransient();
containerDefinition.MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();

static TemplateModule ProvideTemplateModule() {
  var builder = new TemplateModule.Builder();
  builder.AddDecorator<SomeComponentSpec, SomeComponent>();
  return builder.Build();
}
```

Decorator rules:

- Register the runtime component in DI, usually as transient.
- Register the `TemplateModule` provider.
- Add the decorator mapping between the spec or source component and the runtime component.
- Check existing game or repository decorators before creating a new pattern.
- If the decorator mechanism is unclear, ask before implementing it.

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
- Ask instead of guessing when project intent is unclear.

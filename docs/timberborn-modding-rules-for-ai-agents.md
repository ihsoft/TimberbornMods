# Timberborn Modding Rules for AI Agents

This document defines general Timberborn modding rules and practical knowledge for AI agents working on this repository.

Use this together with:

- `csharp-formatting-rules-for-ai-agents.md`
- `timberborn-ui-toolkit-notes-for-ai-agents.md` for in-game UI Toolkit work

The C# formatting file controls code style.
This file controls Timberborn-specific modding practices that are broadly useful across mods.

## Ask when intent is unclear

Do not guess project intent.

Code shows implementation.
It does not always show design intent.

Do not ask merely because more than one implementation is possible. When the remaining choices are internal,
low-risk, and reversible, choose the smallest evidence-supported approach and state any material tradeoff.

Ask when the choice would materially affect task scope, public API, compatibility, player-visible behavior, persisted
data, external state, or the cost of reversing the change, and the intended direction cannot be established from
available evidence.

Examples:

- Should this functionality be generic or feature-specific?
- Should this code be reused elsewhere?
- Is this intended to become infrastructure?
- Is backward compatibility important?
- Is there an existing design decision behind the current implementation?

A short question is preferable when the unresolved choice would commit the project to a materially different
direction.

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

### Loose-file blueprint packages

For loose `.blueprint.json` files owned directly by a mod package, treat the compatibility-lane directory as the path
root. A blueprint referenced as `Buildings/...` or `TemplateCollections/...` must be packaged at that path relative to
the lane. Do not insert a `Blueprints/` prefix merely because extracted game assets use that organizational directory;
use such a prefix only when the owning export pipeline explicitly requires and produces it.

Before real-game validation, inspect every changed `BlockObjectSpec` and verify that `Blocks.Count` equals
`Size.X * Size.Y * Size.Z`. Use the closest stock blueprint to establish the meaning and order of block entries rather
than padding an incomplete list mechanically. A missing entry can fail during eager preview creation before the mod's
intended behavior can be tested.

### Ground-level entrances and unfinished models

In the confirmed current game lifecycle, a ground-level building entrance without `DoorstepSpawnDisablerSpec` requires
a valid unfinished-model transform. Doorstep spawning parents its object to `BuildingModel.UnfinishedModel.transform`
without treating a missing model as valid.

`PlaceFinished` controls placement state; it does not remove this lifecycle dependency. Even a temporary
`PlaceFinished` prototype must provide a valid `UnfinishedModelName` and referenced model when doorstep spawning remains
enabled. Disable doorstep spawning only when the building intentionally does not need it and current architecture
evidence supports that decision.

## Unity-generated assets

When adding generated Unity `.meta` files, run `git diff --check` or `git diff --cached --check` before committing.

Unity can leave trailing whitespace on empty YAML values in generated `.meta` files. If whitespace check reports this,
trim only the trailing whitespace from the affected `.meta` files before staging. Do not change asset GUIDs, importer
settings, bundle names, or other Unity metadata as part of this cleanup.

## Localization

For player-facing text, localization files and keys, `ILoc` usage, translations, and localized UI strings, follow
`agent-knowledge/Timberborn-Localization-Operational-Knowledge-v1.md`.

## Logging

When logging from repository code, use `DebugEx` or `HostedDebugLog`, not direct `UnityEngine.Debug`.

Use `DebugEx` for general repository or service logs.

Use `HostedDebugLog` when the message is logically attached to a game entity or hosted component, usually a
`BaseComponent`.

Prefer placeholder arguments such as `{0}` over string interpolation so repository logging helpers can format messages
consistently:

```csharp
DebugEx.Warning("Failed to read save version from {0}: {1}", selectedSave.DisplayName, e);
HostedDebugLog.Warning(component, "Signal '{0}' is not available on the building.", signalName);
```

Avoid:

```csharp
Debug.LogWarning($"Failed to read save version from {selectedSave.DisplayName}: {e}");
```

## Harmony patching practices

Prefer small, targeted patches.

When patching Timberborn game methods, prefer `nameof(TargetType.Method)` over string method names whenever the
publicized game assemblies make the target method compile-visible. This lets compilation catch renamed or removed game
methods after a Timberborn update.

Do not trust `_DecompiledGame` accessibility alone when deciding whether a game member requires a string-based Harmony
patch or reflection. Decompiled references show the original game assemblies before MSBuild publicizer changes. If the
project publicizes the target assembly, verify against the publicized compile artifact or try compiling with direct
access before falling back to string method names or `AccessTools`.

If publicized direct access is not available for the target method, use the existing reflection or `AccessTools`
approach instead of forcing `nameof`.

Organize Harmony patches by target game class, not by feature.

A patch targeting `MyClass` should live in `MyClassPatch.cs`, and the patch class should be named `MyClassPatch`.

Do not put patches for different target game classes in the same file. If one feature patches several game classes, use
separate `TargetClassPatch.cs` files.

When patching multiple methods on the same target type, prefer one patch class annotated with
`[HarmonyPatch(typeof(TargetType))]` and method-level `[HarmonyPatch(nameof(TargetType.Method))]` attributes, unless a
different shape is required by the target signature or patch discovery.

When a Harmony patch delegates into a normal DI-created class, service, or helper, expose the patch-called entrypoint as
`internal` unless a broader public API is intentionally needed.

Place patch-called entrypoints near the top of that class because they are still an internal API surface. Keep the real
implementation methods private when they are only used by that entrypoint.

For UI patches:

- Prefer postfix patches when possible.
- Avoid replacing original behavior unless necessary.
- When a patch replaces a UI flow, keep the Harmony patch thin when possible. The patch should intercept and delegate to
  a normal DI-created class; the dialog, view, or service should own UI loading, validation, button behavior, and state.
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

## Mod settings

For new ModSettings work, use the current repository settings style:

- declare local `const string ...LocKey` values near the top of the settings class;
- use `BaseSettings<T>`;
- use callback-backed static setting values when runtime code needs static access;
- use `...Internal` naming for public `ModSetting` properties when a wrapper/static runtime value exposes the setting;
- keep visible labels and tooltips in localization files instead of inline strings.

`BaseSettings<T>` is available through TimberDev. If the target mod does not already reference the needed TimberDev
settings helper, add the normal dependency/import instead of falling back to an older settings style.

Do not mechanically rename existing released settings classes or public `ModSetting` properties only to match style.
`ModSettings.Core.ModSettingsOwner` persists settings using keys based on the mod id, settings owner class name, and
public property name. Renaming a settings owner class or public `ModSetting` property can reset players' persisted
settings.

Treat such renames as setting migrations, not formatting cleanup. Before renaming existing settings in released mods,
ask the user whether resetting those settings is acceptable or implement an explicit migration or stable custom id.

## UI manipulation

For UI Toolkit elements, hiding existing elements with `DisplayStyle.None` is a safe first approach.

Example:

```csharp
element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
```

This is often safer than rebuilding lists or modifying source collections.

When UI observes changing gameplay state, distinguish value updates from structural updates.

A value-only change should update existing UI state only: label text, icon state, enabled/disabled state, progress,
selection value, or other stable visual properties. It must not clear containers, recreate controls, rebuild dropdown
items, reset selection, steal focus, or reconstruct the visual hierarchy unless the UI structure actually changed.

When adding related mod settings, check both runtime behavior and settings UI state. If a setting only has an effect
when another mode, toggle, or parent setting is enabled, disable the dependent control through the setting descriptor,
such as `.SetEnableCondition(...)`, instead of leaving an active-looking control whose value is currently ignored.
Labels and tooltips for dependent settings should describe the condition or mode in which the setting applies.

Use structural UI rebuilds only when the shape of the UI changes, such as items being added, removed, renamed,
reordered, or when a different target object requires a different set of controls.

For frequently changing runtime values, prefer separate events or callbacks for structural changes and value-only
changes. Name them so the difference is visible to future agents.

When adding or changing in-game UI assets, first find the closest existing game or mod UI element with the same role.
Reuse its UI Toolkit component type and classes before hand-styling controls. For example, prefer the same
`NineSliceTextField`, button classes, validation classes, and template structure used by nearby dialogs over recreating
input backgrounds and padding manually.

For UI Toolkit details, follow `timberborn-ui-toolkit-notes-for-ai-agents.md`.

If a UI pattern is common enough to document, base repository UI notes on verified local evidence from game UXML, USS,
sprites, classes, and existing mod code. External or AI-generated UI cheat sheets may suggest search terms, but do not
copy them into repository rules or treat them as authoritative until the pattern is confirmed against the current game
assets or code.

## Accessing private fields and publicizer

Before using Harmony `AccessTools` for private/internal game members, inspect the mod project file.

Some mods use `BepInEx.AssemblyPublicizer.MSBuild` and publicized game references.
This repository intentionally uses publicized Timberborn assemblies as a normal modding tool. Publicized direct access is
not a last-resort failure mode.

Example project-file pattern:

```xml
<PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.2" PrivateAssets="all" />
<Reference Include="..\Dependencies\GameRoot\Timberborn_Data\Managed\Timberborn.*.dll" Publicize="true" />
<Reference Include="..\Dependencies\GameRoot\Timberborn_Data\Managed\UnityEngine.UIElementsModule.dll" Publicize="true" />
```

Prefer public game APIs first. If no suitable public API exists and the target assembly is publicized, direct access to
publicized private or internal members is an accepted repository practice.

Do not add reflection, Harmony `AccessTools`, or complex local reimplementations only to avoid touching a publicized
private/internal member. Use direct publicized access when it is simpler and compile-visible.

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

Keep publicized private/internal access localized behind a helper or narrow adapter when the member is version-fragile,
used in hot paths, or likely to need game-version-specific handling.

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

## Game API drift

When fixing a mod after Timberborn game API drift, do more than update signatures until the code compiles.

If the mod subclasses, wraps, replaces, patches, or decorates a stock game component or service, compare the fresh
`_DecompiledGame` implementation with the modded implementation. Preserve new stock behavior unless the user explicitly
wants to override it. Constructor changes often come with behavior changes, so verify the updated stock logic before
using old fallback values or copied logic.

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

Use a generic `Configurator` only when the registered bindings and patches are the same for every context on the class.

When the set differs by context, split into context-specific configurators such as `ConfiguratorForGame`,
`ConfiguratorForMainMenu`, or `ConfiguratorForMapEditor`, and keep each context's bindings and patches in the matching
configurator. Do not hide context-specific branching inside one generic configurator.

Any type defined by a package that must participate in DI must be registered by the package configurator.

When adding a dialog, view, service, or patch helper that can be instantiated from more than one game area, verify every
context that can create it. Include inherited constructor dependencies from base classes. If the type is used from both
main-menu UI and in-game UI, its package bindings and any static dependency bridge must cover the required contexts
explicitly instead of assuming the `Game` context is enough.

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
- Ask instead of guessing when project intent is unclear.

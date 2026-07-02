# Timberborn UI Toolkit Notes for AI Agents

These notes are a verified quick reference for Timberborn UI Toolkit work in this repository.

They are based on current local evidence from decompiled `Timberborn.CoreUI` classes, extracted game UXML/USS assets,
and existing mod UI code. External UI cheat sheets may suggest search terms, but do not treat them as authoritative
until the pattern is verified against current game assets or code.

Use this together with `timberborn-modding-rules-for-ai-agents.md`.

## Verify First

Before adding or changing UI:

1. Find the closest existing game or mod UI with the same role.
2. Inspect its UXML, USS, and code.
3. Reuse the same `Timberborn.CoreUI` element type and USS classes where practical.
4. Verify that the required stylesheets are available in the UXML or loaded view.

Useful local references:

```text
_DecompiledGame/Timberborn.CoreUI/
_ExtractedGameAssets/UI/Views/
ModsUnityProject/Assets/Resources/UI/Views/
ModsUnityProject/Assets/Mods/<ModName>/AssetBundles/Resources/UI/Views/
TimberDev/UI/UiFactory.cs
```

## Common Controls

Prefer Timberborn controls for game-like UI.

Use `Timberborn.CoreUI.LocalizableButton` for buttons with localized text. Apply the same button classes used by the
nearest game UI, such as `menu-button`, `menu-button--medium`, `menu-button--stretched`, `button-game`, or
`button-square`.

Use `Timberborn.CoreUI.NineSliceButton` for icon buttons, non-localized buttons, or buttons whose label is managed in
code. Do not default to Unity's standard `Button` for new game-like controls unless the nearby stock UI uses it for the
same role.

Use `Timberborn.CoreUI.LocalizableLabel` for localized text. Use standard `Label` only for truly static, generated, or
code-owned text that is not player-facing localization.

Use `Timberborn.CoreUI.NineSliceTextField` for text inputs and add the `text-field` class. For large multiline inputs,
check nearby use of `text-field--large`, `game-text-*`, scroll classes, and multiline attributes.

Use `Timberborn.CoreUI.NineSliceVisualElement` for themed containers that need a nine-slice background or border.
Examples include containers using `sliced-border`, `sliced-border--nontransparent`, `bg-box--green`,
`bg-sub-box--green`, or `entity-sub-panel`.

Other verified `Timberborn.CoreUI` controls exist, such as `LocalizableToggle`, `LocalizableSlider`,
`LocalizableSliderInt`, `NineSliceIntegerField`, `NineSliceFloatField`, and `IntegerSliderFactory`. Before using them,
inspect a nearby game example or decompiled factory/initializer and follow its pattern.

## Common Style Classes

Core text classes from `CoreStyle.uss`:

```text
text--default
text--grey
text--bold
text--big
text--header
```

Game/entity UI text classes from `CommonStyle.uss`:

```text
game-text-small
game-text-normal
game-text-big
game-text-heading
game-text-title
game-text--red
text--centered
text--yellow
```

Layout helper classes from `CoreStyle.uss`:

```text
content-centered
content-row-centered
content-row-centered--no-grow
grow-centered
```

Dialog and menu classes from `CoreStyle.uss`:

```text
box
box__content-margin
box__input
menu-button
menu-button--medium
menu-button--centered
menu-button--stretched
wide-menu-button
```

Common container and button classes:

```text
sliced-border
sliced-border--nontransparent
bg-box--green
bg-box--red
bg-box--brown
bg-sub-box--green
entity-sub-panel
button-game
button-square
button-square--small
button-square--large
```

Do not hardcode font sizes, colors, borders, or backgrounds when a nearby stock class already provides the role.

## Dialogs And Boxes

For menu/dialog-like screens, inspect stock templates such as:

```text
ModsUnityProject/Assets/Resources/UI/Views/Common/NamedBoxTemplate.uxml
ModsUnityProject/Assets/Resources/UI/Views/Game/GameOptionsBox.uxml
ModsUnityProject/Assets/Resources/UI/Views/Modding/ModManagerBox.uxml
```

Use `LocalizableButton` plus `menu-button` variants for ordinary dialog actions.

Use `NineSliceTextField` plus `text-field` / `box__input` for dialog text inputs.

Use existing dialog box services and `PanelStack` patterns when creating runtime dialogs. Do not hand-roll panel
lifecycle if the game or TimberDev has a suitable dialog helper.

## Entity Panel Fragments

For right-side entity panel fragments, inspect stock entity panel UXML and existing mod fragments before styling.

Common patterns:

```text
NineSliceVisualElement + bg-sub-box--green + entity-sub-panel
LocalizableLabel or Label + entity-panel__text
NineSliceButton or LocalizableButton + button-game + entity-panel__text
NineSliceTextField + text-field
```

`TimberDev/UI/UiFactory.cs` contains verified helper methods for creating themed entity-panel labels, text fields,
buttons, toggles, and centered fragments. Use or copy its pattern only when it fits the current mod and UI context.

## Standard Unity Controls

Standard Unity UI Toolkit elements still appear in stock UXML for structural roles, lists, close buttons, and special
cases. Do not ban them globally.

However, for new game-like controls that players interact with, prefer the Timberborn `CoreUI` element and style class
used by the closest matching stock UI. Standard `Button`, `TextField`, or unstyled `VisualElement` should be a deliberate
choice backed by a nearby pattern, not the default.

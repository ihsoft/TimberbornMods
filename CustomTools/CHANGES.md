# v1.3.1 (2/20/2026)
* [Fix] The game crashes if the non-stock game factions are in play.

# v1.3.0 (2/18/2026)
* [Change] Custom tools will no longer be shown in map editor.
* [Feature] Introduce ability to define custom bindings.
* [Feature] Added "Demolish building" custom key bindings. You can it up via the game settings UI.
* [Feature] Added "Adjustable Platform" tool. Now, you can choose what kind of platform you're going to place without switching the tool. It can be bound to a hot key.
* [Feature] Added "Levee or Dam" tool. It allows placing either of the elements without switching the tool. It can be bound to a hot key.
* [Feature] Added "Path+" tool. It's a regular "path" tool, except it supports the undo action. It can be bound to a hot key.
* [Feature] Changes, made by the "Adjustable Platform", "Levee or Dam", and "Path+" tools, can be undone. Don't exit the tool, and `Ctrl+Z` hotkey will do the trick.

# v1.2.1 (2/12/2026)
* [Change] Update to support game `v1.0.10.0`.

# v1.2.0 (12/17/2025)
* [Feature] Add keybindings for the CustomTools tools. Go to the stock game "Bindings" dialog and set your bindings.

# v1.1.1 (12/16/2025)
* [Change] Allow custom code to initialize tool with a spec.
* [Feature] Improve error handling in the tools and groups specs.

# v1.1.0 (12/12/2025)
* [Change] Use "Moddable Groups" mod as an engine for the bottom bar tools. This mod is now required.
* [Feature] Subgroups supported.

# v1.0.0 (12/7/2025)
* [Feature] Pause/Resume and Finish Now tools.
* [Feature] Can create custom groups in left, middle, and right sections.
* [Feature] Modders can create their own tools. Check README file.
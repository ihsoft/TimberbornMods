# Overview

This is a place for the dependency packages. Setup symlinks or copy files before building the
projects.

## Setup folders:

1. `GameRoot` should point to the game's root folder. E.g. `C:\Program Files (x86)\Steam\steamapps\common\Timberborn`.
2. `Workshop` should point to the Timberborn Workshop folder. E.g. `C:\Program Files (x86)\Steam\steamapps\workshop\content\1062090`.

The `1062090` ID is likely fixed and assosciated with the game.

The projects refer the subscribed mods (dependencies) by their IDs:

* `3284904751` - Harmony.
* `3288241660` - TimberApi.
* `3283831040` - ModSettings.

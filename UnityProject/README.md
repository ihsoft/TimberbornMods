# Overview

This project is intended to be used "as-is" after checking in from GitHub. All the key packages
are already pre-installed and configured. However, as the game and the dependent mods are updated,
so needs this project too.

## Before you start

Unity tends to hang on the assembly update. To prevent it (or at least make it less frequent), add
the following command line arguments:

- `-DisableDirectoryMonitor`
- `-disable-assembly-updater`

## Updating to the new game version

Do not just copy DLLs from the game's folder! It's important to restore the right meta files.
Otherwise, the stock buildings prefabs won't load and any new prefabs made may not work fine in
the game.

1. Run the editor.
2. Go to "Tools > ThunderKit > Settings".
3. Choose "ThunderKit Settings" menu and provide the valid path to the game in the "Game Path"
   input field.
4. Click "Import" and let the progress complete (can take some time). The kit will ask to restart
   the editor, don't! Just exit the editor.
5. Rollback the changes in `packages-lock.json` file since import breaks the package dependencies.
6. Rollback the changes in `Timberborn/package.json` and update version in it to the current game's
   version.
7. Run the editor and check if there are no errors on load.

The game's libraries are installed as package "Timberborn". It can be seen in the Package Manager.

## Updating TimberAPI

1. Ensure the game version is up to date.
2. Go to the game's `BepInEx/plugins/timberapi/TimberApi/core` folder and copy all the content
   into the Unity packages folder located at `Packages/TimberAPI`.
3. Update the version in the `package.json`.

The API's libraries are installed as package "TimberAPI". It can be seen in the Package Manager.

## Updating TimberCommons

1. Ensure the game and TimberAPI are up to date.
2. Build the latest version of TimberCommons.
3. Copy `TimberCommons.dll` and `CHANGES.md` files into `Package/TimberCommons` folder.
4. Update the version in the `package.json`.

The library is installed as package "TimberCommons". It can be seen in the Package Manager.

**ATTENTION:** Do **not** touch meta file for the DLL! It's important to keep it unchanged since
it contain the GUID under which the prefabs can address the components.

## Making Asset Bundles

The bundles are what Timber mods consume to load the prefabs and other resources. To build asset
bundles for the mods from this repo:

1. In the Unity editor, open `Assets` folder and find asset `pipeline`.
2. In the inspector, click "Execute".
3. The bundles will be created in the `AssetBundles` folder.
4. Copy the new bundles into the appropriate mod `Asset` subfolder.

## Working on the Other Mods

Given it's up to date, this setup is fully equipped for modding. If you want to learn how
to adjust any mod or grab and modify the stock buildings, go to the `Mods` folder and read
the `README.md`.

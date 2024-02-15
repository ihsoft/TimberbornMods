# Overview

**ATTENTION:** This project cannot be used "as-is" from Github! Read below and follow the
required steps before even trying to open it.

This folder contains a Unity project that is used to build assets for all the mods. For the proper
work it should migrate to the current version of Unity used in the game.

**Do not submit game or mod DLLs to Github!** Add them locally, create the assets and build
bundles. Only submit *.meta files for the used scripts since they contain GUIDs by which the
script is referred.

**HINT:** Unity tends to hang on the assembly update. To prevent it (or at least make it less
frequent) add the following command line arguments:

- `-DisableDirectoryMonitor`
- `-disable-assembly-updater`

**HINT:** If Unity hangs during assembly updates (no progress shown), give it 5 minutes and then
just kill it from the Task Manager. Then, restart. It usually works fine, but may need 2-3
attempts before the editor starts.

**HINT:** It would be a very good idea to move this folder to a fast SSD drive. Use junctions to
map the folder from the main Git repository to SSD (if the repository is not already there).

## Getting Game DLLs

Before doing any work, you must import the current game DLLs. It is done via ThunderKit.

1. Run the editor.
2. Go to "Tools > ThunderKit > Settings".
3. Choose "ThunderKit Settings" menu and provide the valid path to the game in the "Game Path"
   input field.
4. Click "Import" and let the progress complete (can take some time). The kit will ask to restart
   the editor, do it.
5. Go to "Window > Package Manager". You should see a group for "DefaultCompany" that has an item
   "Timberborn". If it's there, then it worked as expected.
6. Now you have all the game's DLLs at `Packages/Timberborn`.

This setup is enough for creating custom cursors (e.g. for the `Automation` mod).

## Committing Changes to the Project

If some scripts or related stuff needs to be committed, then it's better to isolate the changes
that were made by ThunderKit. Save the project, go to "Package Manager", remove the Timberborn
package, and close the editor. The state that you get on disk is safe to be committed to Github.

In this setup, some assets in the project may become invalid, but as long as you don't modify or
build them, it's not a problem. Repeat steps from the above article to bring the game's stuff
back and the assets will become functional again.

## Making Asset Bundles

The bundles are what Timber mods consume to load the prefabs and other resources. Every resource
must be tagged to the specific bundle to be packed there. It's selected in the inspector, at the
very bottom line named "AssetBundle".

To build the bundles (they are built all at once), go to "Assets > Build AssetBundles". If there
were no changes, nothing will happen. Otherwise, there will be a progress dialog presented and
it may take some time. This will create bundles at `Assets/AssetBundles`. Copy the bundles to
the appropriate mod's asset folder, renaming the file if needed (they get names from the asset
bundle name).

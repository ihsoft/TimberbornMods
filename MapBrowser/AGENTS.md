# MapBrowser Agent Instructions

These instructions apply to work under `MapBrowser/`. Follow the repository-wide instructions and UI, Unity, Steam,
and validation rules as applicable.

## Package Refresh Contract

MapBrowser is split between the external C# project and Unity-owned package data under
`ModsUnityProject/Assets/Mods/MapBrowser`. Refresh the real local package with Unity export first, then build
`MapBrowser/MapBrowser.csproj` into the exported package so `Scripts/MapBrowser.dll` and `Scripts/MapBrowser.xml`
exist after any export that may clean the destination.

## Workshop Map Metadata Contract

Distinguish Steam subscription from payload download. In an authenticated running Steam client,
`SteamUGC.DownloadItem(itemId, true)` may populate the local UGC cache for an unsubscribed map without
`SteamUGC.SubscribeItem(...)`. Use that only for MapBrowser metadata inspection, not as a general anonymous Workshop
download assumption.

For exact map-size metadata, first use `SteamUGC.GetItemInstallInfo(...)` as a cheap cache existence/location check.
Apply cache checks lazily to visible rows or selected details, session-cache attempted checks and successful sizes, and
do not scan or parse the complete Workshop index synchronously.

Read cached map metadata through Timberborn's map deserialization path, such as `MapDeserializer` plus
`MapMetadataSerializer`, instead of inventing a second map-metadata parser. Metadata-only downloads must not call
`MapRepository.NotifyMapRepositoryChanged()`; they should not make the downloaded payload appear as a normal game map.

Do not generalize this authenticated-client behavior to anonymous SteamCMD, server-side indexers, or game-server UGC
jobs. Those contexts need their own evidence.

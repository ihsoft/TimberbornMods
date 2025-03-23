# Overview

This mod is made for the developers who create mods with UI for Timberborn. It allows exporting UXML
and USS files from the stock game. Using these files, you can create your own UI for the mods in the
same theme.

# How to use

1. Use [AssetRipper](https://github.com/AssetRipper/AssetRipper) to decompile the game's assets into
   a Unity project. This process will give you almost all resources in the right form, except UI 
   documents (UXML) and the stylesheets (USS).
2. Use this mod to export the UXML and USS files from the game. The path to the resources you can
   learn from the decompiled project. For UXML and USS, Ripper will only create `.asset` files. Give
   the mod the path to the resource and set the output directory to the ripped project. Unity will
   pick up the exported file.

The exported USS files almost always are in a good shape and can be used "as-is." The UXML files
may need some adjustments (Unity throws an error when you try to load it). The most common issues
are:
* A template is not found. Find out what the missing template, find it in the ripped project and
  export.
* A style is not found. Find out what the missing style, find it in the ripped project and export.
  Then, correct the path. Even if the proper USS file was already exported, the path in the UXML
  will always be "current folder." Only a few common styles are exceptions (their location is known
  and the exporting process corrects the path automatically).

# How to start making your own UI

Once you've ripped the project, export the common resources.

Stylesheets (USS):

* `UI/Views/Core/CoreStyle`
* `UI/Views/Common/CommonStyle`
* `UI/Views/Common/EntityPanel/EntityPanelCommonStyle`
* `UI/Views/Game/EntityPanel/EntityPanelGameStyle`

UI documents (UXML):

* `UI/Views/Common/NamedBoxTemplate`
* `UI/Views/Common/IntegerSlider`
* `UI/Views/Core/PreciseSlider`
* `UI/Views/Core/ProgressBar`
* `UI/Views/Core/Dropdown`
* `UI/Views/Core/DropDownItem`
* `UI/Views/Core/DropDownItems`

Now, you can make your entity panel fragments and dialogs. Checkout UXML files in the mod's folder
`Examples` to figure out which elements need what styles.

The `TimberDevStyle` stylesheet (used in some examples) is custom. The stock game doesn't have it.
Thus, you need to pack it into the assets of your mod. The stock USS and UXML files don't need to
be in the mod's assets. You only need them when editing content in Unity.  

Not all game's controls will look in Unity the same way they look in the game. The game does some
post-processing on the controls when creating them. For example, the scroll view will not have the
scrollbars in Unity. They will appear only in the game.

# Hints for the runtime

When loading UXML in the game, you need to understand some key points about how the game is behaving:

* `IAssetLoader.Load()` looks for the resources from the root of the game's asset folder. For example,
  `UI/Views/Common/NamedBoxTemplate`. The resource is loaded "as-is" and will _not_ be initialized. 
  That is, any game-specific logic will not be applied (like localization or scroll views
  initialization).
* `VisualElementLoader.LoadVisualElement()` looks for the resources relative to the `UI/Views` folder. 
  If the resource is a UI document (`VisualTreeAsset`), then it is instantiated, and the first element
  in the hierarchy is picked and initialized. Any extra elements will be ignored! That being said, you
  need to wrap the dialog content into a visual element which is the only root child in the UI
  document.

# Example templates

* `Template-EntityPanel-Fragment.uxml` illustrates how to create a fragment for the entity panel.
  Checkout which styles you need to apply to the _stock_ elements.
* `Template-TimberDev.uxml` is a template the entity panel for the elements that are not in the stock 
  game. You will need to attach the `TimberDevStyle` stylesheet to your UI documents.
* `Template-NamedCustomDialog` is a template for a custom dialog. It shows how to create a dialog
  with a custom title and content. It is a control that you add to the `PanelStack` when a dialog
  needs to be presented.

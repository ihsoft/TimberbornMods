using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.MapItemsUI;
using Timberborn.MapThumbnail;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser;

sealed class MapBrowserDialog : AbstractDialog {
  const string DialogAsset = "IgorZ.MapBrowser/MapBrowserDialog";
  const float PreviewHeight = 180;

  readonly MapItemProvider _mapItemProvider;
  readonly MapThumbnailCache _mapThumbnailCache;
  readonly WorkshopMetadataService _metadataService;
  readonly List<InstalledMap> _maps = [];

  ListView _list;
  Label _status;

  MapBrowserDialog(
      MapItemProvider mapItemProvider, MapThumbnailCache mapThumbnailCache,
      WorkshopMetadataService metadataService) {
    _mapItemProvider = mapItemProvider;
    _mapThumbnailCache = mapThumbnailCache;
    _metadataService = metadataService;
  }

  protected override string DialogResourceName => DialogAsset;

  protected override string CancelButtonName => null;

  protected override string VerifyInput() => null;

  protected override void ApplyInput() { }

  protected override bool CheckHasChanges() => false;

  public override void Show() {
    if (Root != null) {
      return;
    }

    base.Show();
    _status = Root.Q2<Label>("Status");
    _list = Root.Q2<ListView>("InstalledMapsList");
    _list.itemsSource = _maps;
    _list.makeItem = CreateMapRow;
    _list.bindItem = BindMapRow;
    _list.selectionType = SelectionType.None;
    _list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
    RefreshMaps();
    _metadataService.MetadataChanged += OnMetadataChanged;
    _metadataService.EnsureLoaded();
    UpdateStatus();
  }

  public override void Close() {
    if (Root == null) {
      return;
    }
    _metadataService.MetadataChanged -= OnMetadataChanged;
    _list = null;
    _status = null;
    _maps.Clear();
    base.Close();
  }

  VisualElement CreateMapRow() {
    var row = new NineSliceVisualElement {
        style = {
            minHeight = PreviewHeight,
            flexDirection = FlexDirection.Row,
            marginBottom = 8,
            paddingTop = 8,
            paddingRight = 8,
            paddingBottom = 8,
            paddingLeft = 8,
        },
    };
    row.AddToClassList("bg-sub-box--green");
    var preview = new Image {
        name = "Preview",
        scaleMode = ScaleMode.ScaleToFit,
        style = {
            width = 300,
            minWidth = 300,
            height = PreviewHeight,
            marginRight = 14,
        },
    };
    var textColumn = new VisualElement { style = { flexGrow = 1 } };
    var title = new Label { name = "Title", style = { fontSize = 19, unityFontStyleAndWeight = FontStyle.Bold } };
    title.AddToClassList("text--default");
    textColumn.Add(title);
    var description = new Label {
        name = "Description",
        style = { whiteSpace = WhiteSpace.Normal, marginTop = 3 },
    };
    description.AddToClassList("text--default");
    textColumn.Add(description);
    var metadata = new Label {
        name = "Metadata",
        style = { whiteSpace = WhiteSpace.Normal, marginTop = 6 },
    };
    metadata.AddToClassList("text--default");
    textColumn.Add(metadata);
    row.Add(preview);
    row.Add(textColumn);
    row.userData = new RowBinding();
    return row;
  }

  void BindMapRow(VisualElement row, int index) {
    var installedMap = _maps[index];
    var binding = (RowBinding)row.userData;
    binding.Key = installedMap.Key;
    var metadata = _metadataService.Find(installedMap.PublishedFileId);
    var title = metadata?.Title ?? installedMap.Map.DisplayName;
    var description = metadata?.DescriptionPlain ?? installedMap.Map.DisplayDescription;
    row.Q<Label>("Title").text = title;
    row.Q<Label>("Description").text = string.IsNullOrWhiteSpace(description) ? "No description" : description;
    row.Q<Label>("Metadata").text = FormatMetadata(installedMap, metadata);

    var preview = row.Q<Image>("Preview");
    preview.image = _mapThumbnailCache.GetThumbnail(installedMap.Map.MapFileReference);
    if (metadata?.PreviewUrl != null) {
      var requestedKey = binding.Key;
      _metadataService.GetPreview(metadata.PreviewUrl, texture => {
        if (texture != null && binding.Key == requestedKey && preview.panel != null) {
          preview.image = texture;
        }
      });
    }
  }

  void RefreshMaps() {
    _maps.Clear();
    _maps.AddRange(_mapItemProvider.GetCustomMaps()
        .Select(map => new InstalledMap(map, FindPublishedFileId(map.MapFileReference.Path)))
        .OrderBy(map => map.Map.DisplayName, StringComparer.OrdinalIgnoreCase));
    _list?.RefreshItems();
  }

  void OnMetadataChanged() {
    UpdateStatus();
    _list?.RefreshItems();
  }

  void UpdateStatus() {
    if (_status == null) {
      return;
    }
    var steamMaps = _maps.Count(map => map.PublishedFileId != null);
    var matchedMaps = _maps.Count(map => _metadataService.Find(map.PublishedFileId) != null);
    var suffix = _metadataService.Loading
        ? " Loading public Workshop metadata..."
        : _metadataService.Error != null ? $" Workshop metadata unavailable: {_metadataService.Error}" : string.Empty;
    _status.text = $"Maps: {_maps.Count}; Steam installations: {steamMaps}; matched metadata: {matchedMaps}.{suffix}";
  }

  static string FormatMetadata(InstalledMap installedMap, WorkshopItemMetadata metadata) {
    var size = installedMap.Map.Size is { } mapSize ? $"{mapSize.x}x{mapSize.y}" : "unknown";
    if (metadata == null) {
      var source = installedMap.PublishedFileId == null ? "local user map" : $"Steam {installedMap.PublishedFileId}";
      return $"Source: {source} | Size: {size} | Path: {installedMap.Map.MapFileReference.Path}";
    }

    var categories = string.Join(", ", metadata.Categories.Select(category =>
        $"{category.Category}:{category.Score} [{string.Join(", ", category.Evidence)}]"));
    var visualFeatures = string.Join(", ", metadata.VisualPercentiles.OrderBy(feature => feature.Key).Select(feature =>
        $"{feature.Key}={feature.Value:0.000} (raw {GetVisualScore(metadata, feature.Key):0.000})"));
    var builder = new StringBuilder();
    builder.Append($"Steam ID: {metadata.PublishedFileId} | Creator: {metadata.CreatorSteamId} | Size: {size}");
    builder.Append($" | Votes: +{metadata.VotesUp}/-{metadata.VotesDown} | Score: {metadata.Score:0.###}");
    builder.Append($"\nCreated: {metadata.CreatedAtUtc:u} | Updated: {metadata.UpdatedAtUtc:u}");
    builder.Append($" | Primary: {metadata.PrimaryCategory} | Tags: {string.Join(", ", metadata.Tags)}");
    if (categories.Length > 0) {
      builder.Append($" | Categories: {categories}");
    }
    if (visualFeatures.Length > 0) {
      builder.Append($"\nVisual: {visualFeatures} | Labels: {string.Join(", ", metadata.VisualLabels)}");
      builder.Append($" | Images: {metadata.VisualImageCount} ({metadata.VisualGalleryImageCount} gallery, ");
      builder.Append($"{metadata.VisualMissingImageCount} missing) | Stale: {metadata.VisualStale}");
    }
    if (metadata.GalleryUrls.Count > 0 || metadata.GalleryCollectionState != null) {
      builder.Append($"\nGallery: {metadata.GalleryCollectionState}; {metadata.GalleryUrls.Count} images");
      if (metadata.GalleryCheckedAtUtc.HasValue) {
        builder.Append($"; checked {metadata.GalleryCheckedAtUtc.Value:u}");
      }
    }
    return builder.ToString();
  }

  static float GetVisualScore(WorkshopItemMetadata metadata, string feature) {
    return metadata.VisualScores.TryGetValue(feature, out var score) ? score : 0;
  }

  static string FindPublishedFileId(string mapPath) {
    if (string.IsNullOrWhiteSpace(mapPath)) {
      return null;
    }
    var directory = new FileInfo(mapPath).Directory;
    for (var depth = 0; directory != null && depth < 3; depth++, directory = directory.Parent) {
      if (directory.Name.Length >= 8 && directory.Name.All(char.IsDigit)) {
        return directory.Name;
      }
    }
    return null;
  }

  sealed record InstalledMap(MapItem Map, string PublishedFileId) {
    public string Key => Map.MapFileReference.Path ?? Map.MapFileReference.Name;
  }

  sealed class RowBinding {
    public string Key { get; set; }
  }
}

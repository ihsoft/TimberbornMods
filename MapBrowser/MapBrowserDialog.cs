using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.MapItemsUI;
using Timberborn.MapRepositorySystem;
using Timberborn.MapThumbnail;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser;

sealed class MapBrowserDialog : AbstractDialog {
  const string DialogAsset = "IgorZ.MapBrowser/MapBrowserDialog";
  const string DeleteMapPromptLocKey = "LoadMapPanel.DeleteMapPrompt";
  const float PreviewHeight = 180;

  readonly MapItemProvider _mapItemProvider;
  readonly MapThumbnailCache _mapThumbnailCache;
  readonly MapRepository _mapRepository;
  readonly WorkshopMetadataService _metadataService;
  readonly WorkshopSubscriptionService _subscriptionService;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly List<InstalledMap> _maps = [];

  ListView _list;

  MapBrowserDialog(
      MapItemProvider mapItemProvider, MapThumbnailCache mapThumbnailCache, MapRepository mapRepository,
      WorkshopMetadataService metadataService, WorkshopSubscriptionService subscriptionService,
      ITooltipRegistrar tooltipRegistrar) {
    _mapItemProvider = mapItemProvider;
    _mapThumbnailCache = mapThumbnailCache;
    _mapRepository = mapRepository;
    _metadataService = metadataService;
    _subscriptionService = subscriptionService;
    _tooltipRegistrar = tooltipRegistrar;
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
    _list = Root.Q2<ListView>("InstalledMapsList");
    _list.itemsSource = _maps;
    _list.makeItem = CreateMapRow;
    _list.bindItem = BindMapRow;
    _list.selectionType = SelectionType.None;
    _list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
    RefreshMaps();
    _metadataService.MetadataChanged += OnMetadataChanged;
    _metadataService.EnsureLoaded();
  }

  public override void Close() {
    if (Root == null) {
      return;
    }
    _metadataService.MetadataChanged -= OnMetadataChanged;
    _list = null;
    _maps.Clear();
    base.Close();
  }

  VisualElement CreateMapRow() {
    var row = new NineSliceVisualElement {
        style = {
            height = PreviewHeight,
            flexDirection = FlexDirection.Row,
            marginBottom = 8,
            paddingTop = 8,
            paddingRight = 8,
            paddingBottom = 8,
            paddingLeft = 8,
        },
    };
    row.AddToClassList("bg-sub-box--green");
    row.AddToClassList("list-view__item-background");
    var binding = new RowBinding();
    row.userData = binding;
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
    var textColumn = new VisualElement {
        style = { flexGrow = 1, height = PreviewHeight, overflow = Overflow.Hidden },
    };
    var title = new Label { name = "Title", style = { fontSize = 19, unityFontStyleAndWeight = FontStyle.Bold } };
    title.AddToClassList("text--default");
    textColumn.Add(title);
    var analysis = new Label {
        name = "Analysis",
        style = {
            whiteSpace = WhiteSpace.Normal,
            marginTop = 2,
            fontSize = 13,
            color = new Color(0.72f, 0.76f, 0.72f),
        },
    };
    analysis.AddToClassList("text--default");
    _tooltipRegistrar.Register(analysis, () => binding.Tooltip);
    textColumn.Add(analysis);
    var freshness = new Label {
        name = "Freshness",
        style = { whiteSpace = WhiteSpace.Normal, marginTop = 3 },
    };
    freshness.AddToClassList("text--default");
    textColumn.Add(freshness);
    var description = new Label {
        name = "Description",
        style = {
            whiteSpace = WhiteSpace.Normal,
            marginTop = 4,
            flexGrow = 1,
            flexShrink = 1,
            overflow = Overflow.Hidden,
        },
    };
    description.AddToClassList("text--default");
    description.RegisterCallback<GeometryChangedEvent>(_ => FitDescription(description, binding.Description));
    textColumn.Add(description);
    var actionButton = new NineSliceButton {
        name = "ActionButton",
        style = {
            position = Position.Absolute,
            right = 8,
            bottom = 8,
            paddingTop = 2,
            paddingRight = 8,
            paddingBottom = 2,
            paddingLeft = 8,
        },
    };
    actionButton.AddToClassList("button-game");
    actionButton.AddToClassList("game-text-small");
    actionButton.ToggleDisplayStyle(false);
    _tooltipRegistrar.Register(actionButton, () => binding.ActionTooltip);
    actionButton.clicked += () => RemoveMap(binding, row, actionButton);
    var removedOverlay = new Label {
        name = "RemovedOverlay",
        text = "Removed",
        pickingMode = PickingMode.Ignore,
        style = {
            position = Position.Absolute,
            left = 0,
            right = 0,
            top = 0,
            bottom = 0,
            fontSize = 24,
            unityFontStyleAndWeight = FontStyle.Bold,
            unityTextAlign = TextAnchor.MiddleCenter,
            backgroundColor = new Color(0.05f, 0.08f, 0.07f, 0.75f),
        },
    };
    removedOverlay.AddToClassList("text--default");
    removedOverlay.ToggleDisplayStyle(false);
    row.Add(preview);
    row.Add(textColumn);
    row.Add(actionButton);
    row.Add(removedOverlay);
    row.RegisterCallback<PointerEnterEvent>(_ => {
      if (binding.Map is { Removed: false }) {
        actionButton.ToggleDisplayStyle(true);
      }
    });
    row.RegisterCallback<PointerLeaveEvent>(_ => actionButton.ToggleDisplayStyle(false));
    return row;
  }

  void BindMapRow(VisualElement row, int index) {
    var installedMap = _maps[index];
    var binding = (RowBinding)row.userData;
    binding.Key = installedMap.Key;
    binding.Map = installedMap;
    binding.ActionText = installedMap.PublishedFileId != null ? "Unsubscribe" : "Delete";
    binding.ActionTooltip = installedMap.PublishedFileId != null
        ? "Unsubscribe from this Steam Workshop map.\nYou can subscribe to it again later."
        : "Permanently delete this local map file.\nThis cannot be undone.";
    var metadata = _metadataService.Find(installedMap.PublishedFileId);
    var title = GetDisplayTitle(installedMap);
    var description = metadata?.DescriptionPlain ?? installedMap.Map.DisplayDescription;
    row.Q<Label>("Title").text = $"{title} ({GetMapSize(installedMap)})";
    var descriptionLabel = row.Q<Label>("Description");
    binding.Description = string.IsNullOrWhiteSpace(description) ? "No description" : NormalizeDescription(description);
    descriptionLabel.text = binding.Description;
    descriptionLabel.schedule.Execute(() => FitDescription(descriptionLabel, binding.Description));
    var analysis = row.Q<Label>("Analysis");
    analysis.text = metadata != null ? FormatCompactAnalysis(metadata) : "Source: Local";
    analysis.ToggleDisplayStyle(metadata != null || installedMap.PublishedFileId == null);
    binding.Tooltip = metadata != null ? FormatAnalysisTooltip(metadata) : null;
    var freshness = row.Q<Label>("Freshness");
    freshness.text = FormatFreshness(installedMap, metadata);
    freshness.ToggleDisplayStyle(installedMap.PublishedFileId != null && (metadata == null || metadata.VisualStale));
    ApplyRemovedState(row, binding, installedMap.Removed);

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
        .Select(map => new InstalledMap(map, FindPublishedFileId(map.MapFileReference.Path))));
    SortMaps();
    _list?.RefreshItems();
  }

  void OnMetadataChanged() {
    SortMaps();
    _list?.Rebuild();
  }

  void SortMaps() {
    _maps.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
        GetDisplayTitle(left), GetDisplayTitle(right)));
  }

  string GetDisplayTitle(InstalledMap installedMap) {
    return _metadataService.Find(installedMap.PublishedFileId)?.Title ?? installedMap.Map.DisplayName;
  }

  static string GetMapSize(InstalledMap installedMap) {
    return installedMap.Map.Size is { } mapSize ? $"{mapSize.x}x{mapSize.y}" : "unknown";
  }

  static string FormatCompactAnalysis(WorkshopItemMetadata metadata) {
    var terrain = GetVisualLevel(
        metadata, "ruggedness", "Flat", "Mostly flat", "Mixed", "Rugged", "Mountainous");
    var valleys = GetVisualLevel(
        metadata, "canyonness", "Open", "Mostly open", "Mixed", "Narrow valleys", "Canyons");
    var water = GetVisualLevel(
        metadata, "water_dominance", "Dry", "Little water", "Moderate water", "Water-rich", "Water-dominated");
    var forests = GetVisualLevel(
        metadata, "forest_density", "Barren", "Sparse", "Moderate forests", "Forested", "Dense forest");
    var landform = GetVisualLevel(
        metadata, "islandness", "Mainland", "Mostly connected", "Mixed", "Fragmented", "Islands");
    var layout = GetVisualLevel(
        metadata, "artificial_layout", "Natural", "Mostly natural", "Mixed", "Structured", "Geometric");
    return $"Terrain: {terrain}, {valleys}, {water}, {forests} | Landform: {landform} | Layout: {layout}";
  }

  static string FormatAnalysisTooltip(WorkshopItemMetadata metadata) {
    var terrain = GetVisualLevel(
        metadata, "ruggedness", "Flat", "Mostly flat", "Mixed", "Rugged", "Mountainous");
    var valleys = GetVisualLevel(
        metadata, "canyonness", "Open", "Mostly open", "Mixed", "Narrow valleys", "Canyons");
    var water = GetVisualLevel(
        metadata, "water_dominance", "Dry", "Little water", "Moderate water", "Water-rich", "Water-dominated");
    var landform = GetVisualLevel(
        metadata, "islandness", "Mainland", "Mostly connected", "Mixed", "Fragmented", "Islands");
    var forests = GetVisualLevel(
        metadata, "forest_density", "Barren", "Sparse", "Moderate forests", "Forested", "Dense forest");
    var layout = GetVisualLevel(
        metadata, "artificial_layout", "Natural", "Mostly natural", "Mixed", "Structured", "Geometric");
    var builder = new StringBuilder();
    builder.AppendLine("Our analysis");
    builder.AppendLine($"Terrain: {terrain}");
    builder.AppendLine($"Valleys: {valleys}");
    builder.AppendLine($"Water: {water}");
    builder.AppendLine($"Landform: {landform}");
    builder.AppendLine($"Forests: {forests}");
    builder.AppendLine($"Layout: {layout}");
    builder.Append($"Based on {metadata.VisualImageCount} Workshop "
        + (metadata.VisualImageCount == 1 ? "image" : "images"));
    return builder.ToString();
  }

  static string GetVisualLevel(
      WorkshopItemMetadata metadata, string feature, string veryLow, string low, string middle, string high,
      string veryHigh) {
    if (!metadata.VisualPercentiles.TryGetValue(feature, out var percentile)) {
      return "Unknown";
    }
    return percentile switch {
        < 0.2f => veryLow,
        < 0.4f => low,
        < 0.6f => middle,
        < 0.8f => high,
        _ => veryHigh,
    };
  }

  static void FitDescription(Label label, string fullText) {
    if (string.IsNullOrEmpty(fullText) || label.contentRect.width <= 0 || label.contentRect.height <= 0) {
      return;
    }
    if (MeasureTextHeight(label, fullText) <= label.contentRect.height) {
      if (label.text != fullText) {
        label.text = fullText;
      }
      return;
    }

    var low = 0;
    var high = fullText.Length;
    while (low < high) {
      var middle = (low + high + 1) / 2;
      var candidate = fullText[..middle].TrimEnd() + "…";
      if (MeasureTextHeight(label, candidate) <= label.contentRect.height) {
        low = middle;
      } else {
        high = middle - 1;
      }
    }

    var length = low;
    while (length > 0 && !char.IsWhiteSpace(fullText[length - 1])) {
      length--;
    }
    if (length == 0) {
      length = low;
    }
    var fittedText = fullText[..length].TrimEnd() + "…";
    if (label.text != fittedText) {
      label.text = fittedText;
    }
  }

  static string NormalizeDescription(string description) {
    return Regex.Replace(description, @"\s+", " ").Trim();
  }

  static float MeasureTextHeight(Label label, string text) {
    return label.MeasureTextSize(
        text, label.contentRect.width, VisualElement.MeasureMode.Exactly,
        0, VisualElement.MeasureMode.Undefined).y;
  }

  void RemoveMap(RowBinding binding, VisualElement row, NineSliceButton button) {
    var installedMap = binding.Map;
    if (installedMap == null || installedMap.Removed) {
      return;
    }
    if (installedMap.PublishedFileId == null) {
      ShowLocalMapDeleteConfirmation(binding, row, button, installedMap);
      return;
    }

    button.text = "Unsubscribing...";
    button.SetEnabled(false);
    _subscriptionService.Unsubscribe(installedMap.PublishedFileId, (succeeded, error) => {
      if (succeeded) {
        installedMap.Removed = true;
      }
      if (binding.Map != installedMap) {
        return;
      }
      if (succeeded) {
        ApplyRemovedState(row, binding, removed: true);
      } else {
        button.text = "Retry unsubscribe";
        button.SetEnabled(true);
        Debug.LogError($"MapBrowser: could not unsubscribe from {installedMap.PublishedFileId}: {error}");
      }
    });
  }

  void ShowLocalMapDeleteConfirmation(
      RowBinding binding, VisualElement row, NineSliceButton button, InstalledMap installedMap) {
    var message = string.Format(UiFactory.T(DeleteMapPromptLocKey), installedMap.Map.DisplayName);
    DialogBoxShower.Create()
        .SetMessage(message)
        .SetConfirmButton(() => DeleteLocalMap(binding, row, button, installedMap))
        .SetDefaultCancelButton()
        .Show();
  }

  void DeleteLocalMap(
      RowBinding binding, VisualElement row, NineSliceButton button, InstalledMap installedMap) {
    button.text = "Deleting...";
    button.SetEnabled(false);
    try {
      _mapRepository.DeleteMap(installedMap.Map.MapFileReference);
      installedMap.Removed = true;
      if (binding.Map == installedMap) {
        ApplyRemovedState(row, binding, removed: true);
      }
    } catch (Exception exception) {
      button.text = "Retry delete";
      button.SetEnabled(true);
      Debug.LogError($"MapBrowser: could not delete local map {installedMap.Key}: {exception}");
    }
  }

  static void ApplyRemovedState(VisualElement row, RowBinding binding, bool removed) {
    row.SetEnabled(!removed);
    row.Q<Label>("RemovedOverlay").ToggleDisplayStyle(removed);
    var button = row.Q<NineSliceButton>("ActionButton");
    button.text = binding.ActionText;
    button.SetEnabled(true);
    button.ToggleDisplayStyle(false);
  }

  string FormatFreshness(InstalledMap installedMap, WorkshopItemMetadata metadata) {
    if (metadata?.VisualStale == true) {
      return "Freshness warning: The analysis above describes previously indexed images; the current Workshop "
          + "images have changed and could not be reclassified yet.";
    }
    if (metadata != null || installedMap.PublishedFileId == null) {
      return string.Empty;
    }

    var snapshot = _metadataService.IndexGeneratedAtUtc.HasValue
        ? _metadataService.IndexGeneratedAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
        : "unknown";
    return $"Freshness: This map is not included in the latest index snapshot ({snapshot}). Analysis will become "
        + "available after a later refresh.";
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
    public bool Removed { get; set; }
  }

  sealed class RowBinding {
    public string Key { get; set; }
    public string Tooltip { get; set; }
    public string Description { get; set; }
    public InstalledMap Map { get; set; }
    public string ActionText { get; set; }
    public string ActionTooltip { get; set; }
  }
}

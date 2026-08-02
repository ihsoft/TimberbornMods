using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using Timberborn.MapItemsUI;
using Timberborn.MapRepositorySystem;
using Timberborn.MapThumbnail;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser;

sealed class MapBrowserDialog : AbstractDialog {
  const string DialogAsset = "IgorZ.MapBrowser/MapBrowserDialog";
  const string SearchPanelAsset = "IgorZ.MapBrowser/MapSearchPanel";
  const string DeleteMapPromptLocKey = "LoadMapPanel.DeleteMapPrompt";
  const string DeleteLocKey = "IgorZ.MapBrowser.Action.Delete";
  const string DeleteTooltipLocKey = "IgorZ.MapBrowser.Action.DeleteTooltip";
  const string DeletingLocKey = "IgorZ.MapBrowser.Action.Deleting";
  const string DetailsLocKey = "IgorZ.MapBrowser.Action.Details";
  const string NoDescriptionLocKey = "IgorZ.MapBrowser.Common.NoDescription";
  const string RemovedLocKey = "IgorZ.MapBrowser.Action.Removed";
  const string RetryDeleteLocKey = "IgorZ.MapBrowser.Action.RetryDelete";
  const string RetryUnsubscribeLocKey = "IgorZ.MapBrowser.Action.RetryUnsubscribe";
  const string SourceLocalLocKey = "IgorZ.MapBrowser.Common.SourceLocal";
  const string TitleWithSizeLocKey = "IgorZ.MapBrowser.Browser.TitleWithSize";
  const string UnsubscribeLocKey = "IgorZ.MapBrowser.Action.Unsubscribe";
  const string UnsubscribeTooltipLocKey = "IgorZ.MapBrowser.Action.UnsubscribeTooltip";
  const string UnsubscribingLocKey = "IgorZ.MapBrowser.Action.Unsubscribing";
  const float PreviewHeight = 180;

  static readonly SearchFilter[] SearchFilters = [
    new("ruggedness", ["Flat", "MostlyFlat", "Mixed", "Rugged", "Mountainous"]),
    new("canyonness", ["Open", "MostlyOpen", "Mixed", "NarrowValleys", "Canyons"]),
    new("water_dominance", ["Dry", "LittleWater", "ModerateWater", "WaterRich", "WaterDominated"]),
    new("forest_density", ["Barren", "Sparse", "ModerateForests", "Forested", "DenseForest"]),
    new("islandness", ["Mainland", "MostlyConnected", "Mixed", "Fragmented", "Islands"]),
    new("artificial_layout", ["Natural", "MostlyNatural", "Mixed", "Structured", "Geometric"]),
  ];

  readonly MapItemProvider _mapItemProvider;
  readonly MapThumbnailCache _mapThumbnailCache;
  readonly MapRepository _mapRepository;
  readonly MapDetailsDialog _mapDetailsDialog;
  readonly WorkshopMetadataService _metadataService;
  readonly WorkshopSubscriptionService _subscriptionService;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly DropdownItemsSetter _dropdownItemsSetter;
  readonly List<InstalledMap> _installedMaps = [];
  readonly List<InstalledMap> _searchMatches = [];
  readonly List<InstalledMap> _searchResults = [];
  readonly Dictionary<string, SearchDropdownProvider> _searchFilters = [];

  ListView _list;
  List<InstalledMap> _visibleMaps;
  TextField _searchText;
  VisualElement _searchPanel;
  Label _modeHeading;
  Button _installedTab;
  Button _searchTab;
  Label _matchesLabel;
  Label _pageLabel;
  Button _previousPageButton;
  Button _nextPageButton;
  int _pageIndex;
  int _pageSize = 50;

  MapBrowserDialog(
      MapItemProvider mapItemProvider, MapThumbnailCache mapThumbnailCache, MapRepository mapRepository,
      MapDetailsDialog mapDetailsDialog, WorkshopMetadataService metadataService,
      WorkshopSubscriptionService subscriptionService,
      ITooltipRegistrar tooltipRegistrar, DropdownItemsSetter dropdownItemsSetter) {
    _mapItemProvider = mapItemProvider;
    _mapThumbnailCache = mapThumbnailCache;
    _mapRepository = mapRepository;
    _mapDetailsDialog = mapDetailsDialog;
    _metadataService = metadataService;
    _subscriptionService = subscriptionService;
    _tooltipRegistrar = tooltipRegistrar;
    _dropdownItemsSetter = dropdownItemsSetter;
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
    InitializeModes();
    _list = Root.Q2<ListView>("InstalledMapsList");
    _visibleMaps = _installedMaps;
    _list.itemsSource = _visibleMaps;
    _list.makeItem = CreateMapRow;
    _list.bindItem = BindMapRow;
    _list.selectionType = SelectionType.None;
    _list.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
    RefreshInstalledMaps();
    _metadataService.MetadataChanged += OnMetadataChanged;
    _metadataService.EnsureLoaded();
  }

  public override void Close() {
    if (Root == null) {
      return;
    }
    _metadataService.MetadataChanged -= OnMetadataChanged;
    _list = null;
    _visibleMaps = null;
    _searchText = null;
    _searchPanel = null;
    _modeHeading = null;
    _installedTab = null;
    _searchTab = null;
    _installedMaps.Clear();
    _searchMatches.Clear();
    _searchResults.Clear();
    _searchFilters.Clear();
    base.Close();
  }

  void InitializeModes() {
    var tabs = Root.Q2<VisualElement>("TabButtons");
    tabs.style.flexWrap = Wrap.Wrap;
    _installedTab = UiFactory.CreateButton(
        "IgorZ.MapBrowser.Search.Installed", _ => SetSearchMode(false), classes: ["game-text-small"]);
    _installedTab.style.marginRight = 6;
    _searchTab = UiFactory.CreateButton(
        "IgorZ.MapBrowser.Search.Search", _ => SetSearchMode(true), classes: ["game-text-small"]);
    tabs.Add(_installedTab);
    tabs.Add(_searchTab);
    _modeHeading = Root.Q2<Label>("ModeHeading");
    _searchPanel = Root.Q2<VisualElement>("SearchPanel");
    CreateSearchControls();
    SetSearchMode(false);
  }

  void CreateSearchControls() {
    var searchControls = UiFactory.LoadVisualTreeAsset(SearchPanelAsset);
    _searchPanel.Add(searchControls);
    _searchText = UiFactory.CreateTextField(classes: ["game-text-normal"]);
    _searchText.style.flexGrow = 1;
    _searchText.RegisterValueChangedCallback(_ => ApplySearch());
    searchControls.Q2<VisualElement>("KeywordsField").Add(_searchText);
    foreach (var filter in SearchFilters) {
      BindSearchFilter(searchControls, filter);
    }
    BindSearchPagingControls(searchControls);
  }

  void BindSearchPagingControls(VisualElement searchControls) {
    _matchesLabel = searchControls.Q2<Label>("MatchesLabel");
    var pageSize = searchControls.Q2<Dropdown>("PageSizeDropdown");
    var pageSizeProvider = new SearchDropdownProvider(["25", "50", "100", "200"]);
    pageSizeProvider.SetValue(_pageSize.ToString());
    pageSize.ValueChanged += (_, _) => {
      if (int.TryParse(pageSizeProvider.GetValue(), out var parsedPageSize)) {
        _pageSize = parsedPageSize;
        _pageIndex = 0;
        RefreshSearchPage();
      }
    };
    _dropdownItemsSetter.SetItems(pageSize, pageSizeProvider);
    _previousPageButton = searchControls.Q2<Button>("PreviousPageButton");
    _previousPageButton.clicked += () => ChangePage(-1);
    _pageLabel = searchControls.Q2<Label>("PageLabel");
    _nextPageButton = searchControls.Q2<Button>("NextPageButton");
    _nextPageButton.clicked += () => ChangePage(1);
  }

  void BindSearchFilter(VisualElement searchControls, SearchFilter filter) {
    var dropdown = searchControls.Q2<Dropdown>(filter.Feature + "Dropdown");
    var values = new[] { UiFactory.T("IgorZ.MapBrowser.Search.Any") }
        .Concat(filter.Levels.Select(level => UiFactory.T("IgorZ.MapBrowser.Analysis.Level." + level)))
        .ToArray();
    var provider = new SearchDropdownProvider(values);
    dropdown.ValueChanged += (_, _) => ApplySearch();
    _dropdownItemsSetter.SetItems(dropdown, provider);
    _searchFilters[filter.Feature] = provider;
  }

  void SetSearchMode(bool searchMode) {
    _installedTab.SetEnabled(searchMode);
    _searchTab.SetEnabled(!searchMode);
    _searchPanel.ToggleDisplayStyle(searchMode);
    UpdateModeHeading(searchMode);
    if (searchMode) {
      ApplySearch();
    } else {
      ShowMaps(_installedMaps);
    }
  }

  void UpdateModeHeading(bool searchMode) {
    _modeHeading.text = searchMode
        ? UiFactory.T("IgorZ.MapBrowser.Browser.SearchMaps")
        : $"{UiFactory.T("IgorZ.MapBrowser.Browser.InstalledMaps")} ({_installedMaps.Count})";
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
    var title = new Label {
        name = "Title",
        style = {
            fontSize = 19,
            unityFontStyleAndWeight = FontStyle.Bold,
            whiteSpace = WhiteSpace.NoWrap,
            overflow = Overflow.Hidden,
            textOverflow = TextOverflow.Ellipsis,
        },
    };
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
    var actions = new VisualElement {
        name = "Actions",
        style = {
            position = Position.Absolute,
            right = 8,
            bottom = 8,
            flexDirection = FlexDirection.Row,
        },
    };
    actions.ToggleDisplayStyle(false);
    var detailsButton = (NineSliceButton)UiFactory.CreateButton(
        DetailsLocKey, _ => ShowDetails(binding), (2, 8, 2, 8), ["game-text-small"]);
    detailsButton.name = "DetailsButton";
    detailsButton.style.marginRight = 6;
    detailsButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
    var actionButton = (NineSliceButton)UiFactory.CreateButton(
        DeleteLocKey, button => RemoveMap(binding, row, (NineSliceButton)button),
        (2, 8, 2, 8), ["game-text-small"]);
    actionButton.name = "ActionButton";
    _tooltipRegistrar.Register(actionButton, () => binding.ActionTooltip);
    actionButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
    actions.Add(detailsButton);
    actions.Add(actionButton);
    var removedOverlay = new Label {
        name = "RemovedOverlay",
        text = UiFactory.T(RemovedLocKey),
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
    row.Add(actions);
    row.Add(removedOverlay);
    row.RegisterCallback<PointerEnterEvent>(_ => {
      if (binding.Map is { Removed: false }) {
        actions.ToggleDisplayStyle(true);
      }
    });
    row.RegisterCallback<PointerLeaveEvent>(_ => actions.ToggleDisplayStyle(false));
    row.RegisterCallback<ClickEvent>(_ => ShowDetails(binding));
    return row;
  }

  void BindMapRow(VisualElement row, int index) {
    var installedMap = _visibleMaps[index];
    var binding = (RowBinding)row.userData;
    binding.Key = installedMap.Key;
    binding.Map = installedMap;
    binding.ActionText = installedMap.IsInstalled
        ? UiFactory.T(installedMap.PublishedFileId != null ? UnsubscribeLocKey : DeleteLocKey)
        : string.Empty;
    binding.ActionTooltip = installedMap.IsInstalled
        ? installedMap.PublishedFileId != null
            ? UiFactory.T(UnsubscribeTooltipLocKey)
            : UiFactory.T(DeleteTooltipLocKey)
        : null;
    var metadata = GetMetadata(installedMap);
    var title = GetDisplayTitle(installedMap);
    var description = metadata?.DescriptionPlain ?? installedMap.Map?.DisplayDescription;
    var titleLabel = row.Q<Label>("Title");
    titleLabel.text = installedMap.IsInstalled
        ? UiFactory.T(
            TitleWithSizeLocKey, title,
            GetMapSize(installedMap, UiFactory.T("IgorZ.MapBrowser.Common.Unknown")))
        : title;
    var descriptionLabel = row.Q<Label>("Description");
    binding.Description = string.IsNullOrWhiteSpace(description)
        ? UiFactory.T(NoDescriptionLocKey)
        : NormalizeDescription(description);
    descriptionLabel.text = binding.Description;
    descriptionLabel.schedule.Execute(() => FitDescription(descriptionLabel, binding.Description));
    var analysis = row.Q<Label>("Analysis");
    analysis.text = metadata != null ? FormatCompactAnalysis(metadata) : UiFactory.T(SourceLocalLocKey);
    analysis.ToggleDisplayStyle(metadata != null || installedMap.PublishedFileId == null);
    binding.Tooltip = metadata != null ? FormatAnalysisTooltip(metadata) : null;
    var freshness = row.Q<Label>("Freshness");
    freshness.text = FormatFreshness(installedMap, metadata);
    freshness.ToggleDisplayStyle(installedMap.PublishedFileId != null && (metadata == null || metadata.VisualStale));
    ApplyRemovedState(row, binding, installedMap.Removed);

    var preview = row.Q<Image>("Preview");
    preview.image = installedMap.Map != null
        ? _mapThumbnailCache.GetThumbnail(installedMap.Map.MapFileReference)
        : null;
    if (metadata?.PreviewUrl != null) {
      var requestedKey = binding.Key;
      _metadataService.GetPreview(metadata.PreviewUrl, texture => {
        if (texture != null && binding.Key == requestedKey && preview.panel != null) {
          preview.image = texture;
        }
      });
    }
  }

  void RefreshInstalledMaps() {
    _installedMaps.Clear();
    _installedMaps.AddRange(_mapItemProvider.GetCustomMaps()
        .Select(map => new InstalledMap(map, FindPublishedFileId(map.MapFileReference.Path))));
    SortMaps(_installedMaps);
    if (_visibleMaps == _installedMaps) {
      UpdateModeHeading(false);
      _list?.RefreshItems();
    } else {
      ApplySearch();
    }
  }

  void OnMetadataChanged() {
    SortMaps(_installedMaps);
    if (_visibleMaps == _installedMaps) {
      _list?.Rebuild();
    } else {
      ApplySearch();
    }
  }

  void SortMaps(List<InstalledMap> maps) {
    maps.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
        GetDisplayTitle(left), GetDisplayTitle(right)));
  }

  string GetDisplayTitle(InstalledMap installedMap) {
    return GetMetadata(installedMap)?.Title ?? installedMap.Map?.DisplayName ?? installedMap.PublishedFileId;
  }

  WorkshopItemMetadata GetMetadata(InstalledMap installedMap) {
    return installedMap.Metadata ?? _metadataService.Find(installedMap.PublishedFileId);
  }

  void ApplySearch() {
    _searchMatches.Clear();
    var installedById = _installedMaps
        .Where(map => map.PublishedFileId != null)
        .GroupBy(map => map.PublishedFileId, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    var terms = Regex.Split(_searchText?.value?.Trim() ?? string.Empty, @"\s+")
        .Where(term => term.Length > 0)
        .ToArray();
    foreach (var metadata in _metadataService.Items.Where(item => item.PrimaryCategory == "map")) {
      if (!MatchesText(metadata, terms) || !MatchesFilters(metadata)) {
        continue;
      }
      _searchMatches.Add(installedById.TryGetValue(metadata.PublishedFileId, out var installedMap)
          ? installedMap with { Metadata = metadata }
          : new InstalledMap(null, metadata.PublishedFileId, metadata));
    }
    SortMaps(_searchMatches);
    _pageIndex = 0;
    RefreshSearchPage();
  }

  void RefreshSearchPage() {
    var totalMaps = _metadataService.Items.Count(item => item.PrimaryCategory == "map");
    var pageCount = _searchMatches.Count == 0
        ? 0
        : (_searchMatches.Count + _pageSize - 1) / _pageSize;
    _pageIndex = pageCount == 0 ? 0 : Math.Clamp(_pageIndex, 0, pageCount - 1);
    _searchResults.Clear();
    _searchResults.AddRange(_searchMatches.Skip(_pageIndex * _pageSize).Take(_pageSize));
    if (_matchesLabel != null && _pageLabel != null && _previousPageButton != null && _nextPageButton != null) {
      _matchesLabel.text = UiFactory.T("IgorZ.MapBrowser.Search.MatchCount", _searchMatches.Count, totalMaps);
      _pageLabel.text = UiFactory.T(
          "IgorZ.MapBrowser.Search.Page", pageCount == 0 ? 0 : _pageIndex + 1, pageCount);
      _previousPageButton.SetEnabled(_pageIndex > 0);
      _nextPageButton.SetEnabled(_pageIndex + 1 < pageCount);
    }
    ShowMaps(_searchResults);
  }

  void ChangePage(int delta) {
    _pageIndex += delta;
    RefreshSearchPage();
  }

  static bool MatchesText(WorkshopItemMetadata metadata, IReadOnlyCollection<string> terms) {
    if (terms.Count == 0) {
      return true;
    }
    var searchableText = metadata.Title + "\n" + metadata.DescriptionPlain;
    return terms.All(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
  }

  bool MatchesFilters(WorkshopItemMetadata metadata) {
    foreach (var filter in SearchFilters) {
      var selectedIndex = _searchFilters.GetValueOrDefault(filter.Feature)?.SelectedIndex ?? 0;
      if (selectedIndex == 0) {
        continue;
      }
      if (!metadata.VisualPercentiles.TryGetValue(filter.Feature, out var percentile)
          || GetPercentileBucket(percentile) != selectedIndex - 1) {
        return false;
      }
    }
    return true;
  }

  static int GetPercentileBucket(float percentile) {
    return percentile switch {
        < 0.2f => 0,
        < 0.4f => 1,
        < 0.6f => 2,
        < 0.8f => 3,
        _ => 4,
    };
  }

  void ShowMaps(List<InstalledMap> maps) {
    _visibleMaps = maps;
    if (_list != null) {
      _list.itemsSource = maps;
      _list.Rebuild();
    }
  }

  internal static string GetMapSize(InstalledMap installedMap, string unknown) {
    return installedMap.Map?.Size is { } mapSize ? $"{mapSize.x}x{mapSize.y}" : unknown;
  }

  string FormatCompactAnalysis(WorkshopItemMetadata metadata) {
    var terrain = GetVisualLevel(
        metadata, "ruggedness", "Flat", "MostlyFlat", "Mixed", "Rugged", "Mountainous", UiFactory);
    var valleys = GetVisualLevel(
        metadata, "canyonness", "Open", "MostlyOpen", "Mixed", "NarrowValleys", "Canyons", UiFactory);
    var water = GetVisualLevel(
        metadata, "water_dominance", "Dry", "LittleWater", "ModerateWater", "WaterRich", "WaterDominated", UiFactory);
    var forests = GetVisualLevel(
        metadata, "forest_density", "Barren", "Sparse", "ModerateForests", "Forested", "DenseForest", UiFactory);
    var landform = GetVisualLevel(
        metadata, "islandness", "Mainland", "MostlyConnected", "Mixed", "Fragmented", "Islands", UiFactory);
    var layout = GetVisualLevel(
        metadata, "artificial_layout", "Natural", "MostlyNatural", "Mixed", "Structured", "Geometric", UiFactory);
    return string.Format(
        UiFactory.T("IgorZ.MapBrowser.Analysis.Compact"), terrain, valleys, water, forests, landform, layout);
  }

  internal static string FormatFullAnalysis(WorkshopItemMetadata metadata, UiFactory uiFactory) {
    var terrain = GetVisualLevel(
        metadata, "ruggedness", "Flat", "MostlyFlat", "Mixed", "Rugged", "Mountainous", uiFactory);
    var valleys = GetVisualLevel(
        metadata, "canyonness", "Open", "MostlyOpen", "Mixed", "NarrowValleys", "Canyons", uiFactory);
    var water = GetVisualLevel(
        metadata, "water_dominance", "Dry", "LittleWater", "ModerateWater", "WaterRich", "WaterDominated", uiFactory);
    var landform = GetVisualLevel(
        metadata, "islandness", "Mainland", "MostlyConnected", "Mixed", "Fragmented", "Islands", uiFactory);
    var forests = GetVisualLevel(
        metadata, "forest_density", "Barren", "Sparse", "ModerateForests", "Forested", "DenseForest", uiFactory);
    var layout = GetVisualLevel(
        metadata, "artificial_layout", "Natural", "MostlyNatural", "Mixed", "Structured", "Geometric", uiFactory);
    var analysis = string.Format(
        uiFactory.T("IgorZ.MapBrowser.Analysis.Full"), terrain, valleys, water, landform, forests, layout);
    var imageCountKey = metadata.VisualImageCount == 1
        ? "IgorZ.MapBrowser.Analysis.BasedOnImage"
        : "IgorZ.MapBrowser.Analysis.BasedOnImages";
    return analysis + "\n" + uiFactory.T(imageCountKey, metadata.VisualImageCount);
  }

  string FormatAnalysisTooltip(WorkshopItemMetadata metadata) {
    return UiFactory.T("IgorZ.MapBrowser.Analysis.Tooltip", FormatFullAnalysis(metadata, UiFactory));
  }

  static string GetVisualLevel(
      WorkshopItemMetadata metadata, string feature, string veryLow, string low, string middle, string high,
      string veryHigh, UiFactory uiFactory) {
    if (!metadata.VisualPercentiles.TryGetValue(feature, out var percentile)) {
      return uiFactory.T("IgorZ.MapBrowser.Analysis.Level.Unknown");
    }
    var level = percentile switch {
        < 0.2f => veryLow,
        < 0.4f => low,
        < 0.6f => middle,
        < 0.8f => high,
        _ => veryHigh,
    };
    return uiFactory.T("IgorZ.MapBrowser.Analysis.Level." + level);
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

  internal static string NormalizeDescription(string description) {
    return Regex.Replace(description, @"\s+", " ").Trim();
  }

  static float MeasureTextHeight(Label label, string text) {
    return label.MeasureTextSize(
        text, label.contentRect.width, VisualElement.MeasureMode.Exactly,
        0, VisualElement.MeasureMode.Undefined).y;
  }

  void RemoveMap(RowBinding binding, VisualElement row, NineSliceButton button) {
    var installedMap = binding.Map;
    if (installedMap is not { IsInstalled: true, Removed: false }) {
      return;
    }
    if (installedMap.PublishedFileId == null) {
      ShowLocalMapDeleteConfirmation(binding, row, button, installedMap);
      return;
    }

    button.text = UiFactory.T(UnsubscribingLocKey);
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
        button.text = UiFactory.T(RetryUnsubscribeLocKey);
        button.SetEnabled(true);
        Debug.LogError($"MapBrowser: could not unsubscribe from {installedMap.PublishedFileId}: {error}");
      }
    });
  }

  void ShowDetails(RowBinding binding) {
    if (binding.Map is { Removed: false } installedMap) {
      _mapDetailsDialog.Show(installedMap, () => _list?.RefreshItems());
    }
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
    button.text = UiFactory.T(DeletingLocKey);
    button.SetEnabled(false);
    try {
      _mapRepository.DeleteMap(installedMap.Map.MapFileReference);
      installedMap.Removed = true;
      if (binding.Map == installedMap) {
        ApplyRemovedState(row, binding, removed: true);
      }
    } catch (Exception exception) {
      button.text = UiFactory.T(RetryDeleteLocKey);
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
    button.ToggleDisplayStyle(binding.Map.IsInstalled && !removed);
    row.Q<VisualElement>("Actions").ToggleDisplayStyle(false);
  }

  string FormatFreshness(InstalledMap installedMap, WorkshopItemMetadata metadata) {
    if (metadata?.VisualStale == true) {
      return UiFactory.T("IgorZ.MapBrowser.Freshness.Stale");
    }
    if (metadata != null || installedMap.PublishedFileId == null) {
      return string.Empty;
    }

    var snapshot = _metadataService.IndexGeneratedAtUtc.HasValue
        ? _metadataService.IndexGeneratedAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
        : UiFactory.T("IgorZ.MapBrowser.Common.Unknown");
    return UiFactory.T("IgorZ.MapBrowser.Freshness.Missing", snapshot);
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

  sealed class RowBinding {
    public string Key { get; set; }
    public string Tooltip { get; set; }
    public string Description { get; set; }
    public InstalledMap Map { get; set; }
    public string ActionText { get; set; }
    public string ActionTooltip { get; set; }
  }

  sealed record SearchFilter(string Feature, string[] Levels);

  sealed class SearchDropdownProvider : IDropdownProvider {
    readonly string[] _items;

    public SearchDropdownProvider(string[] items) {
      _items = items;
      Items = items;
      Value = items.FirstOrDefault();
    }

    public IReadOnlyList<string> Items { get; }

    string Value { get; set; }

    public int SelectedIndex => Array.IndexOf(_items, Value);

    public string GetValue() => Value;

    public void SetValue(string value) => Value = value;
  }
}

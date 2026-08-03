// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using IgorZ.MapBrowser.Core;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using Timberborn.MapItemsUI;
using Timberborn.MapRepositorySystem;
using Timberborn.MapThumbnail;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser.CoreUI;

sealed class MapBrowserDialog : AbstractDialog {
  const string AnalysisCompactLocKey = "IgorZ.MapBrowser.Analysis.Compact";
  const string AnalysisFullLocKey = "IgorZ.MapBrowser.Analysis.Full";
  const string AnalysisLevelLocKeyPrefix = "IgorZ.MapBrowser.Analysis.Level.";
  const string AnalysisLevelUnknownLocKey = "IgorZ.MapBrowser.Analysis.Level.Unknown";
  const string BrowserInstalledMapsLocKey = "IgorZ.MapBrowser.Browser.InstalledMaps";
  const string BrowserSearchMapsLocKey = "IgorZ.MapBrowser.Browser.SearchMaps";
  const string CommonUnknownLocKey = "IgorZ.MapBrowser.Common.Unknown";
  const string DialogAsset = "IgorZ.MapBrowser/MapBrowserDialog";
  const string MapRowAsset = "IgorZ.MapBrowser/MapRow";
  const string SearchPanelAsset = "IgorZ.MapBrowser/MapSearchPanel";
  const string SearchAnyLocKey = "IgorZ.MapBrowser.Search.Any";
  const string SearchInstalledLocKey = "IgorZ.MapBrowser.Search.Installed";
  const string SearchMatchCountLocKey = "IgorZ.MapBrowser.Search.MatchCount";
  const string SearchMatchCountWithSnapshotLocKey = "IgorZ.MapBrowser.Search.MatchCountWithSnapshot";
  const string SearchPageLocKey = "IgorZ.MapBrowser.Search.Page";
  const string SearchSearchLocKey = "IgorZ.MapBrowser.Search.Search";
  const string DeleteMapPromptLocKey = "LoadMapPanel.DeleteMapPrompt";
  const string DeleteLocKey = "IgorZ.MapBrowser.Action.Delete";
  const string DeleteTooltipLocKey = "IgorZ.MapBrowser.Action.DeleteTooltip";
  const string DeletingLocKey = "IgorZ.MapBrowser.Action.Deleting";
  const string DownloadingLocKey = "IgorZ.MapBrowser.Action.Downloading";
  const string NoDescriptionLocKey = "MapSelection.NoDescription";
  const string RetryDeleteLocKey = "IgorZ.MapBrowser.Action.RetryDelete";
  const string RetrySubscribeLocKey = "IgorZ.MapBrowser.Action.RetrySubscribe";
  const string RetryUnsubscribeLocKey = "IgorZ.MapBrowser.Action.RetryUnsubscribe";
  const string SourceLocalLocKey = "IgorZ.MapBrowser.Common.SourceLocal";
  const string SubscribeLocKey = "IgorZ.MapBrowser.Action.Subscribe";
  const string SubscribeTooltipLocKey = "IgorZ.MapBrowser.Action.SubscribeTooltip";
  const string SubscribingLocKey = "IgorZ.MapBrowser.Action.Subscribing";
  const string UnsubscribeLocKey = "IgorZ.MapBrowser.Action.Unsubscribe";
  const string UnsubscribeTooltipLocKey = "IgorZ.MapBrowser.Action.UnsubscribeTooltip";
  const string UnsubscribingLocKey = "IgorZ.MapBrowser.Action.Unsubscribing";
  const string FreshnessMissingLocKey = "IgorZ.MapBrowser.Freshness.Missing";
  const string FreshnessStaleLocKey = "IgorZ.MapBrowser.Freshness.Stale";
  static readonly Regex ParenthesizedMapSizeRegex = new(
      @"\(\s*(?<width>\d{1,4})\s*[xX×]\s*(?<height>\d{1,4})\s*\)", RegexOptions.Compiled);
  static readonly Regex MapSizePrefixRegex = new(
      @"^\s*(?:[\(\[]\s*)?(?<width>\d{1,4})\s*[xX×]\s*(?<height>\d{1,4})(?:\s*[\)\]])?\s*(?:[-–—:|]\s*)?",
      RegexOptions.Compiled);
  static readonly Regex ParenthesizedTitlePrefixesRegex = new(
      @"^(?:\s*\([^)]*\))+\s*(?:[-–—:|]\s*)?", RegexOptions.Compiled);
  static readonly Regex ParenthesizedMapSizeSuffixRegex = new(
      @"\s*(?:[-–—:|]\s*)?\(\s*(?<width>\d{1,4})\s*[xX×]\s*(?<height>\d{1,4})\s*\)\s*$",
      RegexOptions.Compiled);

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
  readonly HashSet<string> _removedWorkshopIds = [];
  readonly Dictionary<string, SearchDropdownProvider> _searchFilters = [];

  ListView _list;
  List<InstalledMap> _visibleMaps;
  TextField _searchText;
  VisualElement _searchPanel;
  VisualElement _searchPagingPanel;
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
    _metadataService.MetadataChanged += OnMetadataChanged;
    _subscriptionService.DownloadProgressChanged += OnDownloadProgressChanged;
    _subscriptionService.DownloadCompleted += OnDownloadCompleted;
    _metadataService.EnsureLoaded();
    RefreshInstalledMaps();
  }

  public override void Close() {
    if (Root == null) {
      return;
    }
    _metadataService.MetadataChanged -= OnMetadataChanged;
    _subscriptionService.DownloadProgressChanged -= OnDownloadProgressChanged;
    _subscriptionService.DownloadCompleted -= OnDownloadCompleted;
    _list = null;
    _visibleMaps = null;
    _searchText = null;
    _searchPanel = null;
    _searchPagingPanel = null;
    _modeHeading = null;
    _installedTab = null;
    _searchTab = null;
    _installedMaps.Clear();
    _searchMatches.Clear();
    _searchResults.Clear();
    _removedWorkshopIds.Clear();
    _searchFilters.Clear();
    base.Close();
  }

  void InitializeModes() {
    _installedTab = Root.Q2<NineSliceButton>("InstalledTabButton");
    _installedTab.text = UiFactory.T(SearchInstalledLocKey);
    _installedTab.clicked += () => SetSearchMode(false);
    _searchTab = Root.Q2<NineSliceButton>("SearchTabButton");
    _searchTab.text = UiFactory.T(SearchSearchLocKey);
    _searchTab.clicked += () => SetSearchMode(true);
    _modeHeading = Root.Q2<Label>("ModeHeading");
    _searchPanel = Root.Q2<VisualElement>("SearchPanel");
    _searchPagingPanel = Root.Q2<VisualElement>("SearchPagingPanel");
    CreateSearchControls();
    SetSearchMode(false);
  }

  void CreateSearchControls() {
    var searchControls = UiFactory.LoadVisualTreeAsset(SearchPanelAsset);
    _searchPanel.Add(searchControls);
    _searchText = searchControls.Q2<TextField>("KeywordsTextField");
    _searchText.RegisterValueChangedCallback(_ => ApplySearch());
    foreach (var filter in SearchFilters) {
      BindSearchFilter(searchControls, filter);
    }
    BindSearchPagingControls(_searchPagingPanel);
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
    var values = new[] { UiFactory.T(SearchAnyLocKey) }
        .Concat(filter.Levels.Select(level => UiFactory.T(AnalysisLevelLocKeyPrefix + level)))
        .ToArray();
    var provider = new SearchDropdownProvider(values);
    dropdown.ValueChanged += (_, _) => ApplySearch();
    _dropdownItemsSetter.SetItems(dropdown, provider);
    _searchFilters[filter.Feature] = provider;
  }

  void SetSearchMode(bool searchMode) {
    ReloadInstalledMaps();
    _installedTab.SetEnabled(searchMode);
    _searchTab.SetEnabled(!searchMode);
    _searchPanel.ToggleDisplayStyle(searchMode);
    _searchPagingPanel.ToggleDisplayStyle(searchMode);
    UpdateModeHeading(searchMode);
    if (searchMode) {
      ApplySearch();
    } else {
      ShowMaps(_installedMaps);
    }
  }

  void UpdateModeHeading(bool searchMode) {
    _modeHeading.text = searchMode
        ? UiFactory.T(BrowserSearchMapsLocKey)
        : $"{UiFactory.T(BrowserInstalledMapsLocKey)} ({_installedMaps.Count})";
  }

  VisualElement CreateMapRow() {
    var row = UiFactory.LoadVisualTreeAsset(MapRowAsset);
    var binding = new RowBinding();
    row.userData = binding;
    var analysis = row.Q2<Label>("Analysis");
    _tooltipRegistrar.Register(analysis, () => binding.Tooltip);
    var description = row.Q2<Label>("Description");
    description.RegisterCallback<GeometryChangedEvent>(_ => FitDescription(description, binding.Description));
    var actions = row.Q2<VisualElement>("Actions");
    var detailsButton = row.Q2<Button>("DetailsButton");
    detailsButton.clicked += () => ShowDetails(binding);
    detailsButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
    var actionButton = row.Q2<NineSliceButton>("ActionButton");
    actionButton.clicked += () => ApplyMapAction(binding, row, actionButton);
    _tooltipRegistrar.Register(actionButton, () => binding.ActionTooltip);
    actionButton.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
    row.RegisterCallback<PointerEnterEvent>(_ => {
      if (binding.Map is { Removed: false }) {
        actions.ToggleDisplayStyle(true);
      }
    });
    row.RegisterCallback<PointerLeaveEvent>(_ => {
      if (!binding.Downloading) {
        actions.ToggleDisplayStyle(false);
      }
    });
    row.RegisterCallback<ClickEvent>(evt => {
      if (evt.target is not VisualElement target || !actions.Contains(target)) {
        ShowDetails(binding);
      }
    });
    return row;
  }

  void BindMapRow(VisualElement row, int index) {
    var installedMap = _visibleMaps[index];
    var binding = (RowBinding)row.userData;
    binding.Key = installedMap.Key;
    binding.Map = installedMap;
    binding.WorkshopSubscribed = installedMap.PublishedFileId != null
        && !_removedWorkshopIds.Contains(installedMap.PublishedFileId)
        && (installedMap.IsInstalled || _subscriptionService.IsSubscribed(installedMap.PublishedFileId));
    binding.Downloading = installedMap.PublishedFileId != null
        && _subscriptionService.IsDownloading(installedMap.PublishedFileId);
    binding.ActionText = binding.Downloading
        ? FormatDownloadProgress(installedMap.PublishedFileId)
        : installedMap.PublishedFileId == null
            ? UiFactory.T(DeleteLocKey)
            : UiFactory.T(binding.WorkshopSubscribed ? UnsubscribeLocKey : SubscribeLocKey);
    binding.ActionTooltip = installedMap.PublishedFileId == null
        ? UiFactory.T(DeleteTooltipLocKey)
        : UiFactory.T(binding.WorkshopSubscribed ? UnsubscribeTooltipLocKey : SubscribeTooltipLocKey);
    var metadata = GetMetadata(installedMap);
    var title = FormatMapTitle(installedMap);
    var description = metadata?.DescriptionPlain ?? installedMap.Map?.DisplayDescription;
    var titleLabel = row.Q<Label>("Title");
    titleLabel.text = title;
    var mapSizeBadge = row.Q<Label>("MapSizeBadge");
    var mapSize = GetMapSize(installedMap, metadata, null);
    mapSizeBadge.text = mapSize;
    mapSizeBadge.ToggleDisplayStyle(mapSize != null);
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
    freshness.ToggleDisplayStyle(
        _metadataService.Loaded && installedMap.PublishedFileId != null
        && (metadata == null || metadata.VisualStale));
    ApplyRemovedState(row, binding, installedMap.Removed);
    row.Q<NineSliceButton>("ActionButton").SetEnabled(!binding.Downloading);
    row.Q<VisualElement>("Actions").ToggleDisplayStyle(binding.Downloading);
    row.Q<VisualElement>("SubscriptionBadge").ToggleDisplayStyle(
        _visibleMaps != _installedMaps && binding.WorkshopSubscribed);

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

  void RefreshInstalledMaps(bool resetSearchNavigation = true) {
    ReloadInstalledMaps();
    if (_visibleMaps == _installedMaps) {
      UpdateModeHeading(false);
      _list?.RefreshItems();
    } else {
      ApplySearch(resetSearchNavigation, resetSearchNavigation);
    }
  }

  void ReloadInstalledMaps() {
    _installedMaps.Clear();
    _installedMaps.AddRange(_mapItemProvider.GetCustomMaps()
        .Select(map => new InstalledMap(map, FindPublishedFileId(map.MapFileReference.Path)))
        .Where(map => map.PublishedFileId == null || !_removedWorkshopIds.Contains(map.PublishedFileId)));
    SortMaps(_installedMaps);
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
        GetSortTitle(left), GetSortTitle(right)));
  }

  string GetRawTitle(InstalledMap installedMap) {
    return (GetMetadata(installedMap)?.Title ?? installedMap.Map?.DisplayName ?? installedMap.PublishedFileId).Trim();
  }

  string GetSortTitle(InstalledMap installedMap) {
    var title = RemoveEdgeMapSize(GetRawTitle(installedMap));
    return ParenthesizedTitlePrefixesRegex.Replace(title, string.Empty).TrimStart();
  }

  string FormatMapTitle(InstalledMap installedMap) {
    var rawTitle = GetRawTitle(installedMap);
    if (HasEmbeddedMapSize(rawTitle)) {
      return rawTitle;
    }
    return RemoveEdgeMapSize(rawTitle);
  }

  WorkshopItemMetadata GetMetadata(InstalledMap installedMap) {
    return installedMap.Metadata ?? _metadataService.Find(installedMap.PublishedFileId);
  }

  void ApplySearch(bool resetPage = true, bool resetScroll = true) {
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
    if (resetPage) {
      _pageIndex = 0;
    }
    RefreshSearchPage(resetScroll);
  }

  void RefreshSearchPage(bool resetScroll = true) {
    var totalMaps = _metadataService.Items.Count(item => item.PrimaryCategory == "map");
    var pageCount = _searchMatches.Count == 0
        ? 0
        : (_searchMatches.Count + _pageSize - 1) / _pageSize;
    _pageIndex = pageCount == 0 ? 0 : Math.Clamp(_pageIndex, 0, pageCount - 1);
    _searchResults.Clear();
    _searchResults.AddRange(_searchMatches.Skip(_pageIndex * _pageSize).Take(_pageSize));
    if (_matchesLabel != null && _pageLabel != null && _previousPageButton != null && _nextPageButton != null) {
      _matchesLabel.text = _metadataService.IndexGeneratedAtUtc is { } generatedAtUtc
          ? UiFactory.T(
              SearchMatchCountWithSnapshotLocKey, _searchMatches.Count, totalMaps,
              generatedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))
          : UiFactory.T(SearchMatchCountLocKey, _searchMatches.Count, totalMaps);
      _pageLabel.text = UiFactory.T(SearchPageLocKey, pageCount == 0 ? 0 : _pageIndex + 1, pageCount);
      _previousPageButton.SetEnabled(_pageIndex > 0);
      _nextPageButton.SetEnabled(_pageIndex + 1 < pageCount);
    }
    ShowMaps(_searchResults, resetScroll);
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

  void ShowMaps(List<InstalledMap> maps, bool resetScroll = true) {
    _visibleMaps = maps;
    if (_list != null) {
      _list.itemsSource = maps;
      _list.Rebuild();
      if (resetScroll && maps.Count > 0) {
        _list.schedule.Execute(() => _list?.ScrollToItem(0));
      }
    }
  }

  internal static string GetMapSize(
      InstalledMap installedMap, WorkshopItemMetadata metadata, string unknown) {
    if (installedMap.Map?.Size is { } mapSize) {
      return $"{mapSize.x}x{mapSize.y}";
    }
    var title = metadata?.Title ?? installedMap.Map?.DisplayName ?? string.Empty;
    var match = ParenthesizedMapSizeRegex.Match(title);
    if (!match.Success) {
      match = MapSizePrefixRegex.Match(title);
    }
    return match.Success
        ? $"{match.Groups["width"].Value}x{match.Groups["height"].Value}"
        : unknown;
  }

  internal static string RemoveEdgeMapSize(string title) {
    var trimmedTitle = title?.Trim() ?? string.Empty;
    var match = MapSizePrefixRegex.Match(trimmedTitle);
    if (match.Success) {
      var titleWithoutPrefix = trimmedTitle[match.Length..].TrimStart();
      return titleWithoutPrefix.Length > 0 ? titleWithoutPrefix : trimmedTitle;
    }
    match = ParenthesizedMapSizeSuffixRegex.Match(trimmedTitle);
    if (match.Success) {
      var titleWithoutSuffix = trimmedTitle[..match.Index].TrimEnd();
      return titleWithoutSuffix.Length > 0 ? titleWithoutSuffix : trimmedTitle;
    }
    return trimmedTitle;
  }

  static bool HasEmbeddedMapSize(string title) {
    var trimmedTitle = title?.Trim() ?? string.Empty;
    var match = ParenthesizedMapSizeRegex.Match(trimmedTitle);
    return match.Success && match.Index > 0 && match.Index + match.Length < trimmedTitle.Length;
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
    return string.Format(UiFactory.T(AnalysisCompactLocKey), terrain, valleys, water, forests, landform, layout);
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
    return string.Format(uiFactory.T(AnalysisFullLocKey), terrain, valleys, water, landform, forests, layout);
  }

  string FormatAnalysisTooltip(WorkshopItemMetadata metadata) {
    return FormatFullAnalysis(metadata, UiFactory);
  }

  static string GetVisualLevel(
      WorkshopItemMetadata metadata, string feature, string veryLow, string low, string middle, string high,
      string veryHigh, UiFactory uiFactory) {
    if (!metadata.VisualPercentiles.TryGetValue(feature, out var percentile)) {
      return uiFactory.T(AnalysisLevelUnknownLocKey);
    }
    var level = percentile switch {
        < 0.2f => veryLow,
        < 0.4f => low,
        < 0.6f => middle,
        < 0.8f => high,
        _ => veryHigh,
    };
    return uiFactory.T(AnalysisLevelLocKeyPrefix + level);
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

  void ApplyMapAction(RowBinding binding, VisualElement row, NineSliceButton button) {
    if (binding.Map is { PublishedFileId: not null } installedMap && !binding.WorkshopSubscribed) {
      SubscribeToMap(binding, button, installedMap);
      return;
    }
    RemoveMap(binding, row, button);
  }

  void SubscribeToMap(RowBinding binding, NineSliceButton button, InstalledMap installedMap) {
    button.text = UiFactory.T(SubscribingLocKey);
    button.SetEnabled(false);
    _subscriptionService.Subscribe(installedMap.PublishedFileId, (succeeded, error) => {
      if (binding.Map != installedMap) {
        return;
      }
      if (succeeded) {
        _removedWorkshopIds.Remove(installedMap.PublishedFileId);
        binding.WorkshopSubscribed = true;
        binding.Downloading = true;
        binding.ActionText = FormatDownloadProgress(installedMap.PublishedFileId);
        binding.ActionTooltip = UiFactory.T(UnsubscribeTooltipLocKey);
        button.text = binding.ActionText;
      } else {
        button.text = UiFactory.T(RetrySubscribeLocKey);
        Debug.LogError($"MapBrowser: could not subscribe to {installedMap.PublishedFileId}: {error}");
      }
      button.SetEnabled(!binding.Downloading);
    });
  }

  string FormatDownloadProgress(string publishedFileId) {
    return _subscriptionService.TryGetDownloadProgress(publishedFileId, out var progress)
        ? UiFactory.T(DownloadingLocKey, Mathf.FloorToInt(progress * 100))
        : UiFactory.T(SubscribingLocKey);
  }

  void OnDownloadProgressChanged(string publishedFileId) {
    if (_visibleMaps?.Any(map => map.PublishedFileId == publishedFileId) == true) {
      _list?.RefreshItems();
    }
  }

  void OnDownloadCompleted(string publishedFileId, bool succeeded, string error) {
    if (!succeeded) {
      Debug.LogError($"MapBrowser: could not download {publishedFileId}: {error}");
    }
    RefreshInstalledMaps(resetSearchNavigation: false);
  }

  void RemoveMap(RowBinding binding, VisualElement row, NineSliceButton button) {
    var installedMap = binding.Map;
    if (installedMap is not { Removed: false }) {
      return;
    }
    if (installedMap.PublishedFileId == null) {
      if (!installedMap.IsInstalled) {
        return;
      }
      ShowLocalMapDeleteConfirmation(binding, row, button, installedMap);
      return;
    }
    if (!binding.WorkshopSubscribed) {
      return;
    }

    button.text = UiFactory.T(UnsubscribingLocKey);
    button.SetEnabled(false);
    _subscriptionService.Unsubscribe(installedMap.PublishedFileId, (succeeded, error) => {
      if (succeeded) {
        _removedWorkshopIds.Add(installedMap.PublishedFileId);
        installedMap.Removed = installedMap.IsInstalled && _visibleMaps == _installedMaps;
      }
      if (binding.Map != installedMap) {
        return;
      }
      if (succeeded) {
        binding.WorkshopSubscribed = false;
        if (_visibleMaps != _installedMaps) {
          ApplySearch(resetPage: false, resetScroll: false);
          return;
        }
        if (installedMap.Removed) {
          ApplyRemovedState(row, binding, removed: true);
        } else {
          binding.ActionText = UiFactory.T(SubscribeLocKey);
          binding.ActionTooltip = UiFactory.T(SubscribeTooltipLocKey);
          button.text = binding.ActionText;
          button.SetEnabled(true);
        }
      } else {
        button.text = UiFactory.T(RetryUnsubscribeLocKey);
        button.SetEnabled(true);
        Debug.LogError($"MapBrowser: could not unsubscribe from {installedMap.PublishedFileId}: {error}");
      }
    });
  }

  void ShowDetails(RowBinding binding) {
    if (binding.Map is { Removed: false } installedMap) {
      _mapDetailsDialog.Show(
          installedMap, _visibleMaps != _installedMaps, binding.WorkshopSubscribed, OnDetailsMapRemoved);
    }
  }

  void OnDetailsMapRemoved(InstalledMap installedMap, bool subscribed) {
    if (installedMap.PublishedFileId != null) {
      if (subscribed) {
        _removedWorkshopIds.Remove(installedMap.PublishedFileId);
      } else {
        _removedWorkshopIds.Add(installedMap.PublishedFileId);
      }
    }
    if (_visibleMaps != _installedMaps) {
      installedMap.Removed = false;
      ReloadInstalledMaps();
      ApplySearch(resetPage: false, resetScroll: false);
    } else {
      _list?.RefreshItems();
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
    button.ToggleDisplayStyle((binding.Map.IsInstalled || binding.Map.PublishedFileId != null) && !removed);
    row.Q<VisualElement>("Actions").ToggleDisplayStyle(false);
  }

  string FormatFreshness(InstalledMap installedMap, WorkshopItemMetadata metadata) {
    if (!_metadataService.Loaded) {
      return string.Empty;
    }
    if (metadata?.VisualStale == true) {
      return UiFactory.T(FreshnessStaleLocKey);
    }
    if (metadata != null || installedMap.PublishedFileId == null) {
      return string.Empty;
    }

    var snapshot = _metadataService.IndexGeneratedAtUtc.HasValue
        ? _metadataService.IndexGeneratedAtUtc.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
        : UiFactory.T(CommonUnknownLocKey);
    return UiFactory.T(FreshnessMissingLocKey, snapshot);
  }

  internal static string FindPublishedFileId(string mapPath) {
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
    public bool WorkshopSubscribed { get; set; }
    public bool Downloading { get; set; }
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

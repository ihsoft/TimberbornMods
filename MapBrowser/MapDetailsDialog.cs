using System;
using System.Collections.Generic;
using System.Linq;
using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;
using Timberborn.MapRepositorySystem;
using Timberborn.MapThumbnail;
using Timberborn.TooltipSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.MapBrowser;

sealed class MapDetailsDialog : AbstractDialog {
  const string DialogAsset = "IgorZ.MapBrowser/MapDetailsDialog";
  const string DeleteMapPromptLocKey = "LoadMapPanel.DeleteMapPrompt";
  const string DeleteLocKey = "IgorZ.MapBrowser.Action.Delete";
  const string DeleteTooltipLocKey = "IgorZ.MapBrowser.Action.DeleteTooltip";
  const string DeletingLocKey = "IgorZ.MapBrowser.Action.Deleting";
  const string NoDescriptionLocKey = "IgorZ.MapBrowser.Common.NoDescription";
  const string RemovedLocKey = "IgorZ.MapBrowser.Action.Removed";
  const string RetryDeleteLocKey = "IgorZ.MapBrowser.Action.RetryDelete";
  const string RetryUnsubscribeLocKey = "IgorZ.MapBrowser.Action.RetryUnsubscribe";
  const string SourceLocalLocKey = "IgorZ.MapBrowser.Common.SourceLocal";
  const string UnavailableLocKey = "IgorZ.MapBrowser.Common.Unavailable";
  const string UnsubscribeLocKey = "IgorZ.MapBrowser.Action.Unsubscribe";
  const string UnsubscribeTooltipLocKey = "IgorZ.MapBrowser.Action.UnsubscribeTooltip";
  const string UnsubscribingLocKey = "IgorZ.MapBrowser.Action.Unsubscribing";

  readonly MapThumbnailCache _mapThumbnailCache;
  readonly MapRepository _mapRepository;
  readonly WorkshopMetadataService _metadataService;
  readonly WorkshopLiveDetailsService _liveDetailsService;
  readonly WorkshopSubscriptionService _subscriptionService;
  readonly ITooltipRegistrar _tooltipRegistrar;
  readonly List<string> _imageUrls = [];

  InstalledMap _installedMap;
  Action _removedCallback;
  Image _preview;
  Button _previousImageButton;
  Button _nextImageButton;
  NineSliceButton _removeButton;
  int _imageIndex;

  MapDetailsDialog(
      MapThumbnailCache mapThumbnailCache, MapRepository mapRepository, WorkshopMetadataService metadataService,
      WorkshopLiveDetailsService liveDetailsService, WorkshopSubscriptionService subscriptionService,
      ITooltipRegistrar tooltipRegistrar) {
    _mapThumbnailCache = mapThumbnailCache;
    _mapRepository = mapRepository;
    _metadataService = metadataService;
    _liveDetailsService = liveDetailsService;
    _subscriptionService = subscriptionService;
    _tooltipRegistrar = tooltipRegistrar;
  }

  protected override string DialogResourceName => DialogAsset;

  protected override string CancelButtonName => null;

  protected override string VerifyInput() => null;

  protected override void ApplyInput() { }

  protected override bool CheckHasChanges() => false;

  public void Show(InstalledMap installedMap, Action removedCallback) {
    if (Root != null) {
      return;
    }

    _installedMap = installedMap;
    _removedCallback = removedCallback;
    base.Show();
    BindContent();
  }

  public override void Close() {
    base.Close();
    _installedMap = null;
    _removedCallback = null;
    _preview = null;
    _previousImageButton = null;
    _nextImageButton = null;
    _removeButton = null;
    _imageUrls.Clear();
  }

  void BindContent() {
    var metadata = _installedMap.Metadata ?? _metadataService.Find(_installedMap.PublishedFileId);
    Root.Q2<Label>("Title").text = metadata?.Title ?? _installedMap.Map?.DisplayName;
    Root.Q2<Label>("Description").text = GetDescription(metadata);
    var mapInformation = Root.Q2<Label>("MapInformation");
    mapInformation.text = GetMapInformation();
    var analysis = Root.Q2<Label>("Analysis");
    analysis.text = metadata != null
        ? MapBrowserDialog.FormatFullAnalysis(metadata, UiFactory)
        : UiFactory.T(SourceLocalLocKey);
    _preview = Root.Q2<Image>("Preview");
    _previousImageButton = Root.Q2<Button>("PreviousImageButton");
    _nextImageButton = Root.Q2<Button>("NextImageButton");
    _previousImageButton.clicked += ShowPreviousImage;
    _nextImageButton.clicked += ShowNextImage;
    _removeButton = Root.Q2<NineSliceButton>("RemoveButton");
    _removeButton.text = UiFactory.T(_installedMap.PublishedFileId != null ? UnsubscribeLocKey : DeleteLocKey);
    _removeButton.ToggleDisplayStyle(_installedMap.IsInstalled);
    _tooltipRegistrar.Register(_removeButton, GetRemoveTooltip);
    _removeButton.clicked += RemoveMap;
    BuildImageList(metadata);
    ShowImage(0);
    LoadLiveDetails(mapInformation);
  }

  string GetDescription(WorkshopItemMetadata metadata) {
    var description = metadata?.DescriptionRaw ?? _installedMap.Map?.DisplayDescription;
    return string.IsNullOrWhiteSpace(description)
        ? UiFactory.T(NoDescriptionLocKey)
        : SteamDescriptionFormatter.Format(description);
  }

  string GetMapInformation() {
    var mapSize = MapBrowserDialog.GetMapSize(_installedMap, UiFactory.T("IgorZ.MapBrowser.Common.Unavailable"));
    var size = UiFactory.T("IgorZ.MapBrowser.Details.Size", mapSize);
    if (_installedMap.PublishedFileId == null) {
      return size;
    }
    var unavailable = UiFactory.T(UnavailableLocKey);
    return size + "\n" + UiFactory.T("IgorZ.MapBrowser.Details.VotesUnavailable")
        + "\n" + UiFactory.T("IgorZ.MapBrowser.Details.Subscribers", unavailable);
  }

  void LoadLiveDetails(Label mapInformation) {
    if (_installedMap.PublishedFileId == null) {
      return;
    }

    var requestedMap = _installedMap;
    _liveDetailsService.Query(requestedMap.PublishedFileId, (details, _) => {
      if (details == null || Root == null || _installedMap != requestedMap) {
        return;
      }
      var subscribers = details.Subscribers is { } count ? count.ToString("N0") : UiFactory.T(UnavailableLocKey);
      var mapSize = MapBrowserDialog.GetMapSize(requestedMap, UiFactory.T("IgorZ.MapBrowser.Common.Unknown"));
      mapInformation.text = UiFactory.T("IgorZ.MapBrowser.Details.Size", mapSize)
          + "\n" + UiFactory.T("IgorZ.MapBrowser.Details.Votes", "+" + details.VotesUp, "-" + details.VotesDown)
          + "\n" + UiFactory.T("IgorZ.MapBrowser.Details.Subscribers", subscribers);
    });
  }

  void BuildImageList(WorkshopItemMetadata metadata) {
    _imageUrls.Clear();
    if (!string.IsNullOrWhiteSpace(metadata?.PreviewUrl)) {
      _imageUrls.Add(metadata.PreviewUrl);
    }
    if (metadata?.GalleryUrls != null) {
      _imageUrls.AddRange(metadata.GalleryUrls
          .Where(url => !string.IsNullOrWhiteSpace(url))
          .Where(url => !_imageUrls.Contains(url, StringComparer.Ordinal)));
    }
    var hasGallery = _imageUrls.Count > 1;
    _previousImageButton.ToggleDisplayStyle(hasGallery);
    _nextImageButton.ToggleDisplayStyle(hasGallery);
  }

  void ShowPreviousImage() {
    ShowImage((_imageIndex - 1 + _imageUrls.Count) % _imageUrls.Count);
  }

  void ShowNextImage() {
    ShowImage((_imageIndex + 1) % _imageUrls.Count);
  }

  void ShowImage(int index) {
    _imageIndex = index;
    if (_imageUrls.Count == 0) {
      _preview.image = _installedMap.Map != null
          ? _mapThumbnailCache.GetThumbnail(_installedMap.Map.MapFileReference)
          : null;
      return;
    }

    var requestedMap = _installedMap;
    var requestedIndex = index;
    _metadataService.GetPreview(_imageUrls[index], texture => {
      if (texture != null && Root != null && _installedMap == requestedMap && _imageIndex == requestedIndex) {
        _preview.image = texture;
      }
    });
  }

  string GetRemoveTooltip() {
    return _installedMap?.PublishedFileId != null
        ? UiFactory.T(UnsubscribeTooltipLocKey)
        : UiFactory.T(DeleteTooltipLocKey);
  }

  void RemoveMap() {
    if (_installedMap is not { IsInstalled: true, Removed: false }) {
      return;
    }
    if (_installedMap.PublishedFileId == null) {
      var message = string.Format(UiFactory.T(DeleteMapPromptLocKey), _installedMap.Map.DisplayName);
      DialogBoxShower.Create()
          .SetMessage(message)
          .SetConfirmButton(DeleteLocalMap)
          .SetDefaultCancelButton()
          .Show();
      return;
    }

    _removeButton.text = UiFactory.T(UnsubscribingLocKey);
    _removeButton.SetEnabled(false);
    var installedMap = _installedMap;
    _subscriptionService.Unsubscribe(installedMap.PublishedFileId, (succeeded, error) => {
      if (succeeded) {
        MarkRemoved(installedMap);
      } else if (Root != null && _installedMap == installedMap) {
        _removeButton.text = UiFactory.T(RetryUnsubscribeLocKey);
        _removeButton.SetEnabled(true);
        Debug.LogError($"MapBrowser: could not unsubscribe from {installedMap.PublishedFileId}: {error}");
      }
    });
  }

  void DeleteLocalMap() {
    var installedMap = _installedMap;
    _removeButton.text = UiFactory.T(DeletingLocKey);
    _removeButton.SetEnabled(false);
    try {
      _mapRepository.DeleteMap(installedMap.Map.MapFileReference);
      MarkRemoved(installedMap);
    } catch (Exception exception) {
      _removeButton.text = UiFactory.T(RetryDeleteLocKey);
      _removeButton.SetEnabled(true);
      Debug.LogError($"MapBrowser: could not delete local map {installedMap.Key}: {exception}");
    }
  }

  void MarkRemoved(InstalledMap installedMap) {
    installedMap.Removed = true;
    _removedCallback?.Invoke();
    if (Root != null && _installedMap == installedMap) {
      _removeButton.text = UiFactory.T(RemovedLocKey);
      _removeButton.SetEnabled(false);
    }
  }
}

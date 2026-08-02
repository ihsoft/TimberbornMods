// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Steamworks;
using Timberborn.MapRepositorySystem;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using UnityEngine;

namespace IgorZ.MapBrowser.Core;

sealed class WorkshopSubscriptionService : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton {
  readonly SteamManager _steamManager;
  readonly MapRepository _mapRepository;
  readonly HashSet<PublishedFileId_t> _pendingDownloads = [];

  Callback<DownloadItemResult_t> _downloadResult;
  float _nextProgressUpdate;

  public WorkshopSubscriptionService(SteamManager steamManager, MapRepository mapRepository) {
    _steamManager = steamManager;
    _mapRepository = mapRepository;
  }

  public event Action<string> DownloadProgressChanged;

  public event Action<string, bool, string> DownloadCompleted;

  public void Load() {
    if (_steamManager.Initialized) {
      _downloadResult = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
    }
  }

  public void Unload() {
    _downloadResult?.Dispose();
    _downloadResult = null;
    _pendingDownloads.Clear();
  }

  public void UpdateSingleton() {
    if (_pendingDownloads.Count == 0 || Time.unscaledTime < _nextProgressUpdate) {
      return;
    }

    _nextProgressUpdate = Time.unscaledTime + 0.2f;
    foreach (var itemId in _pendingDownloads) {
      DownloadProgressChanged?.Invoke(itemId.m_PublishedFileId.ToString());
    }
  }

  public bool IsSubscribed(string publishedFileId) {
    return _steamManager.Initialized
        && ulong.TryParse(publishedFileId, out var itemId)
        && (SteamUGC.GetItemState(new PublishedFileId_t(itemId)) & (uint)EItemState.k_EItemStateSubscribed) != 0;
  }

  public bool IsDownloading(string publishedFileId) {
    return ulong.TryParse(publishedFileId, out var itemId)
        && _pendingDownloads.Contains(new PublishedFileId_t(itemId));
  }

  public bool TryGetDownloadProgress(string publishedFileId, out float progress) {
    progress = 0;
    if (!ulong.TryParse(publishedFileId, out var parsedItemId)) {
      return false;
    }

    var itemId = new PublishedFileId_t(parsedItemId);
    if (!_pendingDownloads.Contains(itemId)
        || !SteamUGC.GetItemDownloadInfo(itemId, out var downloaded, out var total) || total == 0) {
      return false;
    }
    progress = Mathf.Clamp01((float)downloaded / total);
    return true;
  }

  public void Subscribe(string publishedFileId, Action<bool, string> callback) {
    if (!TryGetPublishedFileId(publishedFileId, callback, out var itemId)) {
      return;
    }

    var apiCall = SteamUGC.SubscribeItem(itemId);
    if (apiCall == SteamAPICall_t.Invalid) {
      callback(false, "Steam rejected the subscribe request.");
      return;
    }
    var callResult = CallResult<RemoteStorageSubscribePublishedFileResult_t>.Create();
    callResult.Set(apiCall, (result, ioFailure) => {
      if (!ioFailure && result.m_eResult == EResult.k_EResultOK && SteamUGC.DownloadItem(itemId, true)) {
        _pendingDownloads.Add(itemId);
        callback(true, null);
        DownloadProgressChanged?.Invoke(publishedFileId);
        return;
      }
      callback(false, ioFailure ? "Steam I/O failure." : result.m_eResult.ToString());
    });
  }

  public void Unsubscribe(string publishedFileId, Action<bool, string> callback) {
    if (!TryGetPublishedFileId(publishedFileId, callback, out var itemId)) {
      return;
    }

    var apiCall = SteamUGC.UnsubscribeItem(itemId);
    if (apiCall == SteamAPICall_t.Invalid) {
      callback(false, "Steam rejected the unsubscribe request.");
      return;
    }
    var callResult = CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create();
    callResult.Set(apiCall, (result, ioFailure) => {
      var succeeded = !ioFailure && result.m_eResult == EResult.k_EResultOK;
      if (succeeded) {
        _mapRepository.NotifyMapRepositoryChanged();
      }
      callback(succeeded, ioFailure ? "Steam I/O failure." : result.m_eResult.ToString());
    });
  }

  void OnDownloadItemResult(DownloadItemResult_t result) {
    if (!_pendingDownloads.Remove(result.m_nPublishedFileId)) {
      return;
    }

    var publishedFileId = result.m_nPublishedFileId.m_PublishedFileId.ToString();
    var succeeded = result.m_eResult == EResult.k_EResultOK;
    if (succeeded) {
      _mapRepository.NotifyMapRepositoryChanged();
    }
    DownloadProgressChanged?.Invoke(publishedFileId);
    DownloadCompleted?.Invoke(publishedFileId, succeeded, result.m_eResult.ToString());
  }

  bool TryGetPublishedFileId(
      string publishedFileId, Action<bool, string> callback, out PublishedFileId_t itemId) {
    itemId = default;
    if (!_steamManager.Initialized) {
      callback(false, "Steam is not initialized.");
      return false;
    }
    if (!ulong.TryParse(publishedFileId, out var parsedItemId)) {
      callback(false, $"Invalid Steam Workshop ID: {publishedFileId}");
      return false;
    }
    itemId = new PublishedFileId_t(parsedItemId);
    return true;
  }
}

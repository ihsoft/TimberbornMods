// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Steamworks;
using Timberborn.MapMetadataSystem;
using Timberborn.MapRepositorySystem;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;
using UnityEngine;

namespace IgorZ.MapBrowser.Core;

sealed class WorkshopMapSizeService : ILoadableSingleton, IUnloadableSingleton {
  readonly SteamManager _steamManager;
  readonly MapRepository _mapRepository;
  readonly MapDeserializer _mapDeserializer;
  readonly MapMetadataSerializer _mapMetadataSerializer;
  readonly Dictionary<PublishedFileId_t, Vector2Int> _sizes = [];
  readonly Dictionary<PublishedFileId_t, List<Action<Vector2Int?>>> _pendingRequests = [];
  readonly HashSet<PublishedFileId_t> _cacheChecks = [];

  Callback<DownloadItemResult_t> _downloadResult;

  public WorkshopMapSizeService(
      SteamManager steamManager, MapRepository mapRepository, MapDeserializer mapDeserializer,
      MapMetadataSerializer mapMetadataSerializer) {
    _steamManager = steamManager;
    _mapRepository = mapRepository;
    _mapDeserializer = mapDeserializer;
    _mapMetadataSerializer = mapMetadataSerializer;
  }

  public event Action<string> MapSizeChanged;

  public void Load() {
    if (_steamManager.Initialized) {
      _downloadResult = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
    }
  }

  public void Unload() {
    _downloadResult?.Dispose();
    _downloadResult = null;
    _pendingRequests.Clear();
    _cacheChecks.Clear();
    _sizes.Clear();
  }

  public bool IsLoading(string publishedFileId) {
    return TryParseId(publishedFileId, out var itemId) && _pendingRequests.ContainsKey(itemId);
  }

  public bool TryGetCachedSize(string publishedFileId, out Vector2Int size) {
    size = default;
    if (!TryParseId(publishedFileId, out var itemId)) {
      return false;
    }
    if (_sizes.TryGetValue(itemId, out size)) {
      return true;
    }
    if (!_cacheChecks.Add(itemId) || !TryReadSize(itemId, out size)) {
      return false;
    }
    _sizes[itemId] = size;
    return true;
  }

  public void RequestSize(string publishedFileId, Action<Vector2Int?> callback) {
    if (!_steamManager.Initialized || !TryParseId(publishedFileId, out var itemId)) {
      callback(null);
      return;
    }
    if (_sizes.TryGetValue(itemId, out var size)) {
      callback(size);
      return;
    }
    if (TryReadSize(itemId, out size)) {
      CacheSize(itemId, size);
      callback(size);
      return;
    }
    if (_pendingRequests.TryGetValue(itemId, out var callbacks)) {
      callbacks.Add(callback);
      return;
    }

    _pendingRequests[itemId] = [callback];
    if (!SteamUGC.DownloadItem(itemId, true)) {
      Complete(itemId, null);
    }
  }

  void OnDownloadItemResult(DownloadItemResult_t result) {
    if (!_pendingRequests.ContainsKey(result.m_nPublishedFileId)) {
      return;
    }
    if (result.m_eResult != EResult.k_EResultOK
        || !TryReadSize(result.m_nPublishedFileId, out var size)) {
      Complete(result.m_nPublishedFileId, null);
      return;
    }

    CacheSize(result.m_nPublishedFileId, size);
    Complete(result.m_nPublishedFileId, size);
  }

  bool TryReadSize(PublishedFileId_t itemId, out Vector2Int size) {
    size = default;
    try {
      if (!SteamUGC.GetItemInstallInfo(itemId, out _, out var folder, 4096, out _)) {
        return false;
      }
      var mapFile = _mapRepository.GetMapFilesFromDirectory(new DirectoryInfo(folder)).FirstOrDefault();
      if (mapFile == null) {
        return false;
      }
      var mapReference = MapFileReference.FromDisk(mapFile.FullName);
      var metadata = _mapDeserializer.ReadFromMapFile(mapReference, _mapMetadataSerializer);
      if (metadata == null) {
        return false;
      }
      size = new Vector2Int(metadata.Width, metadata.Height);
      return true;
    } catch (Exception exception) {
      Debug.LogWarning($"MapBrowser: could not read cached Workshop map {itemId.m_PublishedFileId}: {exception}");
      return false;
    }
  }

  void CacheSize(PublishedFileId_t itemId, Vector2Int size) {
    _sizes[itemId] = size;
    MapSizeChanged?.Invoke(itemId.m_PublishedFileId.ToString());
  }

  void Complete(PublishedFileId_t itemId, Vector2Int? size) {
    if (!_pendingRequests.Remove(itemId, out var callbacks)) {
      return;
    }
    foreach (var callback in callbacks) {
      callback(size);
    }
  }

  static bool TryParseId(string publishedFileId, out PublishedFileId_t itemId) {
    itemId = default;
    if (!ulong.TryParse(publishedFileId, out var parsedItemId)) {
      return false;
    }
    itemId = new PublishedFileId_t(parsedItemId);
    return true;
  }
}

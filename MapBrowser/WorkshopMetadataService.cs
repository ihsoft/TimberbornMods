using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Timberborn.SingletonSystem;
using UnityEngine;
using UnityEngine.Networking;

namespace IgorZ.MapBrowser;

sealed class WorkshopMetadataService : IUnloadableSingleton, IUpdatableSingleton {
  const string MetadataUrl = "https://ihsoft.github.io/TimberbornMods/search-index.jsonl.gz";

  readonly Dictionary<string, WorkshopItemMetadata> _items = [];
  readonly Dictionary<string, Texture2D> _previewCache = [];
  readonly Dictionary<string, PreviewDownload> _previewDownloads = [];

  UnityWebRequest _metadataRequest;
  Task<Dictionary<string, WorkshopItemMetadata>> _parseTask;

  public event Action MetadataChanged;

  public bool Loading { get; private set; }

  public string Error { get; private set; }

  public void EnsureLoaded() {
    if (Loading || _items.Count > 0 || Error != null) {
      return;
    }

    Loading = true;
    _metadataRequest = UnityWebRequest.Get(MetadataUrl);
    var operation = _metadataRequest.SendWebRequest();
    operation.completed += _ => CompleteMetadataRequest();
  }

  public WorkshopItemMetadata Find(string publishedFileId) {
    return publishedFileId != null && _items.TryGetValue(publishedFileId, out var item) ? item : null;
  }

  public void GetPreview(string url, Action<Texture2D> callback) {
    if (string.IsNullOrWhiteSpace(url)) {
      callback(null);
      return;
    }
    if (_previewCache.TryGetValue(url, out var texture)) {
      callback(texture);
      return;
    }
    if (_previewDownloads.TryGetValue(url, out var existingDownload)) {
      existingDownload.Callbacks.Add(callback);
      return;
    }

    var request = UnityWebRequestTexture.GetTexture(url);
    var download = new PreviewDownload(request, callback);
    _previewDownloads.Add(url, download);
    request.SendWebRequest().completed += _ => CompletePreviewRequest(url, download);
  }

  public void UpdateSingleton() {
    if (_parseTask is not { IsCompleted: true }) {
      return;
    }

    Loading = false;
    if (_parseTask.IsCompletedSuccessfully) {
      foreach (var item in _parseTask.Result) {
        _items[item.Key] = item.Value;
      }
    } else {
      Error = _parseTask.Exception?.GetBaseException().Message ?? "Unknown metadata parsing error";
      Debug.LogError($"MapBrowser: could not parse Workshop metadata: {_parseTask.Exception}");
    }
    _parseTask = null;
    MetadataChanged?.Invoke();
  }

  public void Unload() {
    _metadataRequest?.Abort();
    _metadataRequest?.Dispose();
    foreach (var download in _previewDownloads.Values) {
      download.Request.Abort();
      download.Request.Dispose();
    }
    foreach (var texture in _previewCache.Values) {
      UnityEngine.Object.Destroy(texture);
    }
    _previewDownloads.Clear();
    _previewCache.Clear();
    _items.Clear();
  }

  void CompleteMetadataRequest() {
    try {
      if (_metadataRequest.result != UnityWebRequest.Result.Success) {
        throw new InvalidOperationException(_metadataRequest.error);
      }
      var compressedData = _metadataRequest.downloadHandler.data;
      _parseTask = Task.Run(() => ParseMetadata(compressedData));
    } catch (Exception exception) {
      Loading = false;
      Error = exception.Message;
      Debug.LogError($"MapBrowser: could not load Workshop metadata: {exception}");
      MetadataChanged?.Invoke();
    } finally {
      _metadataRequest.Dispose();
      _metadataRequest = null;
    }
  }

  static Dictionary<string, WorkshopItemMetadata> ParseMetadata(byte[] compressedData) {
    var items = new Dictionary<string, WorkshopItemMetadata>();
    using var compressed = new MemoryStream(compressedData);
    using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
    using var reader = new StreamReader(gzip, Encoding.UTF8);
    while (reader.ReadLine() is { } line) {
      var item = JsonConvert.DeserializeObject<WorkshopItemMetadata>(line);
      if (!string.IsNullOrEmpty(item?.PublishedFileId)) {
        items[item.PublishedFileId] = item;
      }
    }
    return items;
  }

  void CompletePreviewRequest(string url, PreviewDownload download) {
    Texture2D texture = null;
    if (download.Request.result == UnityWebRequest.Result.Success) {
      texture = DownloadHandlerTexture.GetContent(download.Request);
      _previewCache[url] = texture;
    }
    _previewDownloads.Remove(url);
    download.Request.Dispose();
    foreach (var callback in download.Callbacks.ToList()) {
      callback(texture);
    }
  }

  sealed class PreviewDownload {
    public PreviewDownload(UnityWebRequest request, Action<Texture2D> callback) {
      Request = request;
      Callbacks.Add(callback);
    }

    public UnityWebRequest Request { get; }
    public List<Action<Texture2D>> Callbacks { get; } = [];
  }
}

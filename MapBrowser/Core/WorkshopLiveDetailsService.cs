// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using Steamworks;
using Timberborn.SingletonSystem;
using Timberborn.SteamStoreSystem;

namespace IgorZ.MapBrowser.Core;

sealed class WorkshopLiveDetailsService(SteamManager steamManager) : IUnloadableSingleton {
  const int MaxGalleryImages = 8;
  readonly List<PendingQuery> _pendingQueries = [];

  public void Query(string publishedFileId, Action<WorkshopLiveDetails, string> callback) {
    if (!steamManager.Initialized) {
      callback(null, "Steam is not initialized.");
      return;
    }
    if (!ulong.TryParse(publishedFileId, out var itemId)) {
      callback(null, $"Invalid Steam Workshop ID: {publishedFileId}");
      return;
    }

    var query = SteamUGC.CreateQueryUGCDetailsRequest([new PublishedFileId_t(itemId)], 1);
    if (query == UGCQueryHandle_t.Invalid) {
      callback(null, "Steam rejected the Workshop details query.");
      return;
    }
    if (!SteamUGC.SetReturnAdditionalPreviews(query, true)) {
      SteamUGC.ReleaseQueryUGCRequest(query);
      callback(null, "Steam rejected the Workshop gallery request.");
      return;
    }
    var apiCall = SteamUGC.SendQueryUGCRequest(query);
    if (apiCall == SteamAPICall_t.Invalid) {
      SteamUGC.ReleaseQueryUGCRequest(query);
      callback(null, "Steam rejected the Workshop details request.");
      return;
    }

    var pendingQuery = new PendingQuery(query, callback, CompleteQuery);
    _pendingQueries.Add(pendingQuery);
    pendingQuery.Start(apiCall);
  }

  public void Unload() {
    foreach (var pendingQuery in _pendingQueries) {
      pendingQuery.Cancel();
    }
    _pendingQueries.Clear();
  }

  void CompleteQuery(PendingQuery pendingQuery, SteamUGCQueryCompleted_t result, bool ioFailure) {
    _pendingQueries.Remove(pendingQuery);
    try {
      if (ioFailure || result.m_eResult != EResult.k_EResultOK
          || !SteamUGC.GetQueryUGCResult(pendingQuery.Query, 0, out var details)) {
        var error = ioFailure ? "Steam I/O failure." : result.m_eResult.ToString();
        pendingQuery.Callback(null, error);
        return;
      }

      ulong? subscribers = SteamUGC.GetQueryUGCStatistic(
          pendingQuery.Query, 0, EItemStatistic.k_EItemStatistic_NumSubscriptions, out var subscriberCount)
          ? subscriberCount
          : null;
      pendingQuery.Callback(new WorkshopLiveDetails(
          details.m_unVotesUp, details.m_unVotesDown, subscribers, ReadGalleryUrls(pendingQuery.Query)), null);
    } finally {
      pendingQuery.Dispose();
    }
  }

  static IReadOnlyList<string> ReadGalleryUrls(UGCQueryHandle_t query) {
    var urls = new List<string>();
    var previewCount = SteamUGC.GetQueryUGCNumAdditionalPreviews(query, 0);
    for (uint previewIndex = 0; previewIndex < previewCount && urls.Count < MaxGalleryImages; previewIndex++) {
      if (!SteamUGC.GetQueryUGCAdditionalPreview(
              query, 0, previewIndex, out var url, 4096, out _, 1024, out var previewType)
          || previewType != EItemPreviewType.k_EItemPreviewType_Image
          || !IsValidGalleryUrl(url)) {
        continue;
      }
      var normalized = NormalizeGalleryUrl(url);
      if (!urls.Contains(normalized)) {
        urls.Add(normalized);
      }
    }
    return urls;
  }

  static bool IsValidGalleryUrl(string value) {
    return Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.Equals("images.steamusercontent.com", StringComparison.OrdinalIgnoreCase);
  }

  static string NormalizeGalleryUrl(string value) {
    var queryIndex = value.IndexOf('?');
    var baseUrl = queryIndex >= 0 ? value[..queryIndex] : value;
    return baseUrl + "?imw=637&imh=358&ima=fit&impolicy=Letterbox&imcolor=%23000000&letterbox=true";
  }

  sealed class PendingQuery : IDisposable {
    readonly Action<PendingQuery, SteamUGCQueryCompleted_t, bool> _completion;
    readonly CallResult<SteamUGCQueryCompleted_t> _callResult;

    public PendingQuery(
        UGCQueryHandle_t query, Action<WorkshopLiveDetails, string> callback,
        Action<PendingQuery, SteamUGCQueryCompleted_t, bool> completion) {
      Query = query;
      Callback = callback;
      _completion = completion;
      _callResult = CallResult<SteamUGCQueryCompleted_t>.Create();
    }

    public UGCQueryHandle_t Query { get; }
    public Action<WorkshopLiveDetails, string> Callback { get; }

    public void Start(SteamAPICall_t apiCall) {
      _callResult.Set(apiCall, (result, ioFailure) => _completion(this, result, ioFailure));
    }

    public void Cancel() {
      _callResult.Cancel();
      Dispose();
    }

    public void Dispose() {
      SteamUGC.ReleaseQueryUGCRequest(Query);
      _callResult.Dispose();
    }
  }
}

sealed record WorkshopLiveDetails(
    uint VotesUp, uint VotesDown, ulong? Subscribers, IReadOnlyList<string> GalleryUrls);

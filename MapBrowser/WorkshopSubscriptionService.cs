using System;
using Steamworks;
using Timberborn.MapRepositorySystem;
using Timberborn.SteamStoreSystem;

namespace IgorZ.MapBrowser;

sealed class WorkshopSubscriptionService(SteamManager steamManager, MapRepository mapRepository) {
  public void Unsubscribe(string publishedFileId, Action<bool, string> callback) {
    if (!steamManager.Initialized) {
      callback(false, "Steam is not initialized.");
      return;
    }
    if (!ulong.TryParse(publishedFileId, out var itemId)) {
      callback(false, $"Invalid Steam Workshop ID: {publishedFileId}");
      return;
    }

    var apiCall = SteamUGC.UnsubscribeItem(new PublishedFileId_t(itemId));
    if (apiCall == SteamAPICall_t.Invalid) {
      callback(false, "Steam rejected the unsubscribe request.");
      return;
    }
    CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create().Set(apiCall, (result, ioFailure) => {
      var succeeded = !ioFailure && result.m_eResult == EResult.k_EResultOK;
      if (succeeded) {
        mapRepository.NotifyMapRepositoryChanged();
      }
      callback(succeeded, ioFailure ? "Steam I/O failure." : result.m_eResult.ToString());
    });
  }
}

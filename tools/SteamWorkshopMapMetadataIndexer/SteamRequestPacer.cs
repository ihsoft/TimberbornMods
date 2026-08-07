// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

#nullable enable

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed class SteamRequestPacer(
    Action<TimeSpan> delay, Action<string>? log = null,
    TimeSpan? slowModeDelay = null, int successfulRequestsToRecover = 6) {
  readonly TimeSpan _slowModeDelay = slowModeDelay ?? TimeSpan.FromSeconds(15);
  readonly Action<string> _log = log ?? Console.WriteLine;
  int _consecutiveSuccessfulRequests;

  public bool SlowModeActive { get; private set; }

  public int ConsecutiveSuccessfulRequests => _consecutiveSuccessfulRequests;

  public bool ShouldTreatAsTransient(string result) {
    return SlowModeActive && result == "k_EResultFail";
  }

  public void WaitBeforeRequest(TimeSpan delayAlreadyApplied) {
    if (!SlowModeActive || delayAlreadyApplied >= _slowModeDelay) {
      return;
    }
    var remainingDelay = _slowModeDelay - delayAlreadyApplied;
    _log($"Steam slow mode: waiting {remainingDelay.TotalSeconds:0} seconds before the next request.");
    delay(remainingDelay);
  }

  public void RecordTransientFailure(string result) {
    var action = SlowModeActive ? "reset" : "activated";
    SlowModeActive = true;
    _consecutiveSuccessfulRequests = 0;
    _log(
        $"Steam slow mode {action} by {result}; "
        + $"{successfulRequestsToRecover} consecutive successful requests required to recover.");
  }

  public void RecordSuccessfulRequest() {
    if (!SlowModeActive) {
      return;
    }
    _consecutiveSuccessfulRequests++;
    if (_consecutiveSuccessfulRequests < successfulRequestsToRecover) {
      _log(
          $"Steam slow mode: {_consecutiveSuccessfulRequests} / {successfulRequestsToRecover} "
          + "consecutive successful requests.");
      return;
    }
    SlowModeActive = false;
    _consecutiveSuccessfulRequests = 0;
    _log("Steam slow mode ended after consecutive successful requests.");
  }
}

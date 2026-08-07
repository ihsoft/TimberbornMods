// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

#nullable enable

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

sealed class SteamRequestPacer(
    Action<TimeSpan> delay, Action<string>? log = null,
    TimeSpan? normalModeDelay = null, TimeSpan? slowModeDelay = null, int successfulRequestsToRecover = 6) {
  readonly TimeSpan _normalModeDelay = normalModeDelay ?? TimeSpan.Zero;
  readonly TimeSpan _slowModeDelay = slowModeDelay ?? TimeSpan.FromSeconds(15);
  readonly Action<string> _log = log ?? Console.WriteLine;
  int _consecutiveSuccessfulRequests;
  bool _requestStarted;

  public bool SlowModeActive { get; private set; }

  public int ConsecutiveSuccessfulRequests => _consecutiveSuccessfulRequests;

  public bool ShouldTreatAsTransient(string result) {
    return SlowModeActive && result == "k_EResultFail";
  }

  public void WaitBeforeRequest(TimeSpan delayAlreadyApplied) {
    if (!_requestStarted) {
      _requestStarted = true;
      return;
    }

    var requiredDelay = SlowModeActive && _slowModeDelay > _normalModeDelay
        ? _slowModeDelay
        : _normalModeDelay;
    if (delayAlreadyApplied >= requiredDelay) {
      return;
    }
    var remainingDelay = requiredDelay - delayAlreadyApplied;
    var mode = SlowModeActive ? "slow mode" : "normal mode";
    _log($"Steam {mode}: waiting {remainingDelay.TotalSeconds:0} seconds before the next request.");
    delay(remainingDelay);
  }

  public void RecordTransientFailure(string result) {
    _requestStarted = true;
    var action = SlowModeActive ? "reset" : "activated";
    SlowModeActive = true;
    _consecutiveSuccessfulRequests = 0;
    _log(
        $"Steam slow mode {action} by {result}; "
        + $"{successfulRequestsToRecover} consecutive successful requests required to recover.");
  }

  public void RecordSuccessfulRequest() {
    _requestStarted = true;
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

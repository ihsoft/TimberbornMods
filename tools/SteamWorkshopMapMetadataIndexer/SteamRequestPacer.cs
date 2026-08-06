#nullable enable

sealed class SteamRequestPacer(
    Action<TimeSpan> delay, Action<string>? log = null,
    TimeSpan? slowModeDelay = null, int successfulRequestsToRecover = 6) {
  readonly TimeSpan _slowModeDelay = slowModeDelay ?? TimeSpan.FromSeconds(10);
  readonly Action<string> _log = log ?? Console.WriteLine;
  int _consecutiveSuccessfulRequests;

  internal bool SlowModeActive { get; private set; }

  internal int ConsecutiveSuccessfulRequests => _consecutiveSuccessfulRequests;

  internal void WaitBeforeRequest(TimeSpan delayAlreadyApplied) {
    if (!SlowModeActive || delayAlreadyApplied >= _slowModeDelay) {
      return;
    }
    var remainingDelay = _slowModeDelay - delayAlreadyApplied;
    _log($"Steam slow mode: waiting {remainingDelay.TotalSeconds:0} seconds before the next request.");
    delay(remainingDelay);
  }

  internal void RecordTransientFailure(string result) {
    var action = SlowModeActive ? "reset" : "activated";
    SlowModeActive = true;
    _consecutiveSuccessfulRequests = 0;
    _log(
        $"Steam slow mode {action} by {result}; "
        + $"{successfulRequestsToRecover} consecutive successful requests required to recover.");
  }

  internal void RecordSuccessfulRequest() {
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

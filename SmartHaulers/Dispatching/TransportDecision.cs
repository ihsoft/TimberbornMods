// Timberborn Mod: SmartHaulers
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.SmartHaulers.Dispatching;

readonly struct TransportDecision {
  public TransportCandidateScore Winner { get; }
  public TransportCandidateScore RunnerUp { get; }
  public bool HasWinner => Winner.Agent.Worker;
  public bool HasRunnerUp => RunnerUp.Agent.Worker;

  public TransportDecision(TransportCandidateScore winner, TransportCandidateScore runnerUp) {
    Winner = winner;
    RunnerUp = runnerUp;
  }
}

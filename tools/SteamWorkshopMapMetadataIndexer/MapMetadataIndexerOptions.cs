// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

/// <summary>Validated operational limits and paths for one resumable exact-map metadata pass.</summary>
sealed class MapMetadataIndexerOptions {
  /// <summary>Creates the validated paths and operational limits selected by the command-line loader.</summary>
  public MapMetadataIndexerOptions(
      string snapshot, string? previousResults, string output, string workshopDirectory,
      int maxDownloadItems, ulong maxDownloadBytes, TimeSpan requestTimeout, TimeSpan requestDelay,
      TimeSpan slowModeDelay, TimeSpan timeBudget, int maxAnalysisParallelism,
      int steamReconnectAfterDownloads, string? stopRequestFile) {
    Snapshot = snapshot;
    PreviousResults = previousResults;
    Output = output;
    WorkshopDirectory = workshopDirectory;
    MaxDownloadItems = maxDownloadItems;
    MaxDownloadBytes = maxDownloadBytes;
    RequestTimeout = requestTimeout;
    RequestDelay = requestDelay;
    SlowModeDelay = slowModeDelay;
    TimeBudget = timeBudget;
    MaxAnalysisParallelism = maxAnalysisParallelism;
    SteamReconnectAfterDownloads = steamReconnectAfterDownloads;
    StopRequestFile = stopRequestFile;
  }

  /// <summary>The complete Workshop metadata snapshot used to select Map-tagged items.</summary>
  public string Snapshot { get; }

  /// <summary>The optional prior result artifact used for incremental resume and stale preservation.</summary>
  public string? PreviousResults { get; }

  /// <summary>The JSON Lines checkpoint and final output path.</summary>
  public string Output { get; }

  /// <summary>The isolated Steam game-server Workshop download directory.</summary>
  public string WorkshopDirectory { get; }

  /// <summary>The maximum number of payloads downloaded from Steam during this pass.</summary>
  public int MaxDownloadItems { get; }

  /// <summary>The maximum accepted declared and installed size of one payload.</summary>
  public ulong MaxDownloadBytes { get; }

  /// <summary>The maximum wait for one Steam callback.</summary>
  public TimeSpan RequestTimeout { get; }

  /// <summary>The normal minimum spacing between sequential Steam requests.</summary>
  public TimeSpan RequestDelay { get; }

  /// <summary>The minimum request spacing while Steam transient-failure slow mode is active.</summary>
  public TimeSpan SlowModeDelay { get; }

  /// <summary>The total execution budget before the pass checkpoints and stops.</summary>
  public TimeSpan TimeBudget { get; }

  /// <summary>The maximum concurrent analyses of already cached payloads.</summary>
  public int MaxAnalysisParallelism { get; }

  /// <summary>The number of Steam downloads after which the anonymous session is proactively reconnected.</summary>
  public int SteamReconnectAfterDownloads { get; }

  /// <summary>An optional external stop-request file checked at safe checkpoint boundaries.</summary>
  public string? StopRequestFile { get; }
}

// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopIndexing;

/// <summary>Validated command-line configuration for one complete Workshop snapshot collection.</summary>
sealed class WorkshopIndexerOptions {
  /// <summary>Creates the output and Steam callback limits selected by the command-line loader.</summary>
  public WorkshopIndexerOptions(string outputPath, TimeSpan requestTimeout) {
    OutputPath = outputPath;
    RequestTimeout = requestTimeout;
  }

  /// <summary>The JSON Lines file that receives the complete snapshot.</summary>
  public string OutputPath { get; }

  /// <summary>The maximum wait for each Steam callback.</summary>
  public TimeSpan RequestTimeout { get; }
}

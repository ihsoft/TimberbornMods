// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopMapIndexing;

static class Program {
  public static int Main(string[] args) => new MapMetadataIndexer().Run(ParseOptions(args));

  static MapMetadataIndexerOptions ParseOptions(string[] args) {
    var values = new Dictionary<string, string>();
    for (var index = 0; index < args.Length; index += 2) {
      if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal)) {
        throw new ArgumentException("Expected --name value arguments.");
      }
      values[args[index]] = args[index + 1];
    }
    var options = new MapMetadataIndexerOptions(
        Required(values, "--snapshot"), values.GetValueOrDefault("--previous-results"),
        Required(values, "--output"), Required(values, "--workshop-directory"),
        ParseInt(values, "--max-items", 50), ParseUlong(values, "--max-download-bytes", 50_000_000),
        TimeSpan.FromSeconds(ParseInt(values, "--request-timeout-seconds", 120)),
        TimeSpan.FromSeconds(ParseInt(values, "--request-delay-seconds", 0)),
        TimeSpan.FromSeconds(ParseInt(values, "--slow-mode-delay-seconds", 15)),
        TimeSpan.FromSeconds(ParseInt(values, "--time-budget-seconds", 7200)),
        ParseInt(values, "--max-analysis-parallelism", Math.Min(Environment.ProcessorCount, 4)),
        ParseInt(values, "--steam-reconnect-after-downloads", 200),
        values.GetValueOrDefault("--stop-request-file"));
    if (options.MaxDownloadItems < 0 || options.MaxDownloadBytes < 1
        || options.RequestTimeout <= TimeSpan.Zero || options.RequestTimeout > TimeSpan.FromMinutes(10)
        || options.RequestDelay < TimeSpan.Zero || options.RequestDelay > TimeSpan.FromMinutes(1)
        || options.SlowModeDelay <= TimeSpan.Zero || options.SlowModeDelay > TimeSpan.FromMinutes(5)
        || options.TimeBudget < TimeSpan.Zero || options.MaxAnalysisParallelism < 1
        || options.MaxAnalysisParallelism > 16 || options.SteamReconnectAfterDownloads < 0) {
      throw new ArgumentOutOfRangeException(nameof(args), "Invalid numeric option.");
    }
    return options;
  }

  static string Required(IReadOnlyDictionary<string, string> values, string name) {
    return values.GetValueOrDefault(name) ?? throw new ArgumentException($"Missing required option {name}.");
  }

  static int ParseInt(IReadOnlyDictionary<string, string> values, string name, int defaultValue) {
    return values.TryGetValue(name, out var value) ? int.Parse(value) : defaultValue;
  }

  static ulong ParseUlong(IReadOnlyDictionary<string, string> values, string name, ulong defaultValue) {
    return values.TryGetValue(name, out var value) ? ulong.Parse(value) : defaultValue;
  }
}

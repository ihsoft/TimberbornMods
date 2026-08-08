// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

namespace IgorZ.MapBrowser.WorkshopIndexing;

static class Program {
  public static int Main(string[] args) {
    var options = ParseOptions(args);
    return options is null ? 2 : new WorkshopIndexer().Run(options);
  }

  static WorkshopIndexerOptions? ParseOptions(string[] args) {
    var output = Path.Combine(".tools", "workshop-index", "timberborn-workshop-bootstrap.jsonl");
    var requestTimeout = TimeSpan.FromSeconds(120);
    for (var index = 0; index < args.Length; index++) {
      switch (args[index]) {
        case "--output" when index + 1 < args.Length:
          output = args[++index];
          break;
        case "--request-timeout-seconds" when index + 1 < args.Length
            && int.TryParse(args[++index], out var seconds) && seconds > 0:
          requestTimeout = TimeSpan.FromSeconds(seconds);
          break;
        case "--help":
          PrintUsage();
          return null;
        default:
          Console.Error.WriteLine($"Unknown or incomplete argument: {args[index]}");
          PrintUsage();
          return null;
      }
    }
    return new WorkshopIndexerOptions(output, requestTimeout);
  }

  static void PrintUsage() {
    Console.WriteLine("SteamWorkshopIndexer [--output <jsonl>] [--request-timeout-seconds <seconds>]");
  }
}

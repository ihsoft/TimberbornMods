namespace IgorZ.TimberbornMods.Tools.SteamTagUpdating;

static class Program {
  public static int Main(string[] args) {
    var updater = new SteamTagUpdater();
    if (args is ["--diagnose"]) {
      return updater.Diagnose();
    }
    if (args is ["--query", var publishedFileId]) {
      return updater.Query(ulong.Parse(publishedFileId));
    }
    if (args.Length < 2) {
      Console.Error.WriteLine("Usage: SteamTagUpdater --diagnose");
      Console.Error.WriteLine("Usage: SteamTagUpdater <publishedFileId> <tag> [<tag>...]");
      return 2;
    }
    return updater.UpdateTags(ulong.Parse(args[0]), args.Skip(1).ToArray());
  }
}

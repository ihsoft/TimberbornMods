namespace IgorZ.TimberbornMods.Tools.WorkshopDependencyUpdating;

static class Program {
  public static int Main(string[] args) {
    if (args.Length != 3 || args[0] is not ("--dry-run" or "--publish")
        || !ulong.TryParse(args[1], out var parentId) || !ulong.TryParse(args[2], out var childId)) {
      Console.Error.WriteLine("Usage: SteamWorkshopDependencyUpdater --dry-run|--publish <parent-id> <child-id>");
      return 2;
    }
    return new WorkshopDependencyUpdater().Run(args[0] == "--publish", parentId, childId);
  }
}

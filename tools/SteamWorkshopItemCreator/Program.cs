namespace IgorZ.TimberbornMods.Tools.WorkshopItemCreation;

static class Program {
  public static int Main(string[] args) {
    if (args.Length != 2 || args[0] is not ("--dry-run" or "--create")) {
      Console.Error.WriteLine("Usage: SteamWorkshopItemCreator --dry-run <plan.json>");
      Console.Error.WriteLine("Usage: SteamWorkshopItemCreator --create <plan.json>");
      return 2;
    }
    return new WorkshopItemCreator().Run(args[0] == "--create", args[1]);
  }
}

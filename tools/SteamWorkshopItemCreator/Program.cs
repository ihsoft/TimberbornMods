namespace IgorZ.TimberbornMods.Tools.WorkshopItemCreation;

static class Program {
  public static int Main(string[] args) {
    var creator = new WorkshopItemCreator();
    if (args is ["--dry-run", var dryRunPlan]) {
      return creator.Run(false, dryRunPlan);
    }
    if (args is ["--create", var createPlan]) {
      return creator.Run(true, createPlan);
    }
    if (args is ["--recover", var publishedFileId, var needsAgreement, var recoveryPlan]
        && ulong.TryParse(publishedFileId, out var parsedPublishedFileId)
        && bool.TryParse(needsAgreement, out var parsedNeedsAgreement)) {
      return creator.Recover(parsedPublishedFileId, parsedNeedsAgreement, recoveryPlan);
    }

    Console.Error.WriteLine("Usage: SteamWorkshopItemCreator --dry-run <plan.json>");
    Console.Error.WriteLine("Usage: SteamWorkshopItemCreator --create <plan.json>");
    Console.Error.WriteLine(
        "Usage: SteamWorkshopItemCreator --recover <publishedFileId> <needsLegalAgreement> <plan.json>");
    return 2;
  }
}

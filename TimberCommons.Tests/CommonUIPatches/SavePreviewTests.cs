using System.IO;
using System.Text;
using IgorZ.TimberCommons.CommonUIPatches;

namespace TimberCommons.Tests;

static class SavePreviewTests {
  public static void FormatsSavedGameVersions() {
    Assert.Equal("v1.0", GameSaveVersionFormatter.Format("1.0.0.0.12345"));
    Assert.Equal("v1.0.12.7", GameSaveVersionFormatter.Format("1.0.12.7.12345"));
    Assert.Equal("v1.1.2", GameSaveVersionFormatter.Format("1.1.2.0.12345"));
  }

  public static void ReadsFactionIdFromWorldEntry() {
    const string WorldJson = "{\"Singletons\":{\"FactionService\":{\"Id\":\"Folktails\"}}}";
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(WorldJson));

    var factionId = new FactionIdSaveEntryReader().ReadFromSaveEntryStream(stream);

    Assert.Equal("Folktails", factionId);
    Assert.True(stream.CanRead, "The save entry stream must remain open for its owner.");
  }

  public static void RejectsWorldEntryWithoutFactionId() {
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"Singletons\":{}}"));

    Assert.Throws<InvalidDataException>(() => new FactionIdSaveEntryReader().ReadFromSaveEntryStream(stream));
  }
}

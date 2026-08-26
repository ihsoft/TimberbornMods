// Timberborn Mod: Timberborn Commons
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.IO;
using System.Text;
using Newtonsoft.Json;
using Timberborn.SaveSystem;

namespace IgorZ.TimberCommons.CommonUIPatches;

sealed class FactionIdSaveEntryReader : ISaveEntryReader<string> {
  const string FactionIdJsonPath = "Singletons.FactionService.Id";

  public string EntryName => "world.json";

  public string ReadFromSaveEntryStream(Stream entryStream) {
    using var streamReader = new StreamReader(entryStream, Encoding.UTF8, true, 1024, leaveOpen: true);
    using var jsonReader = new JsonTextReader(streamReader) {
        CloseInput = false,
    };
    while (jsonReader.Read()) {
      if (jsonReader.TokenType == JsonToken.String && jsonReader.Path == FactionIdJsonPath) {
        return (string) jsonReader.Value;
      }
    }
    throw new InvalidDataException("FactionService.Id is missing from world.json");
  }
}

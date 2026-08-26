using System.IO;

namespace Timberborn.SaveSystem;

public interface ISaveEntryReader<out T> {
  string EntryName { get; }
  T ReadFromSaveEntryStream(Stream entryStream);
}

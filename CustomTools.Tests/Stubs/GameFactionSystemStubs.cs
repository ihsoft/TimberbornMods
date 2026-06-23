using Timberborn.FactionSystem;

namespace Timberborn.GameFactionSystem;

public class FactionService {
  public FactionSpec Current { get; }

  public FactionService(string factionId) {
    Current = new FactionSpec { Id = factionId };
  }
}

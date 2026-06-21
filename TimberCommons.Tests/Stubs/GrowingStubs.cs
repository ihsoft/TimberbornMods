using Timberborn.BaseComponentSystem;
using Timberborn.TimeSystem;

namespace Timberborn.Growing;

public sealed class Growable : BaseComponent {
  public bool IsGrown { get; set; }
  public float GrowthProgress { get; init; }
  public LivingNaturalResource _livingNaturalResource = new();
  public ITimeTrigger _timeTrigger = new EmptyTimeTrigger();
  public GrowableSpec _growableSpec;

  public void Grow() {
  }
}

public sealed record GrowableSpec {
  public float GrowthTimeInDays { get; init; }
}

public sealed class LivingNaturalResource {
  public bool IsDead { get; set; }
}

sealed class EmptyTimeTrigger : ITimeTrigger {
  public void FastForwardProgress(float progress) {
  }

  public void Reset() {
  }

  public void Resume() {
  }
}

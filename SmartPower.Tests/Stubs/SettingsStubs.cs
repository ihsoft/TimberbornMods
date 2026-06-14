namespace IgorZ.SmartPower.Settings;

static class AttractionConsumerSettings {
  public static bool ShowFloatingIcon { get; set; }
  public static int SuspendDelayMinutes { get; set; }
  public static int ResumeDelayMinutes { get; set; }
}

static class BatteriesSettings {
  public static float BatteryRatioHysteresis { get; set; } = 1f;
}

static class UnmannedConsumerSettings {
  public static bool ShowFloatingIcon { get; set; }
}

static class WorkplaceConsumerSettings {
  public static bool ShowFloatingIcon { get; set; }
  public static int SuspendDelayMinutes { get; set; }
  public static int ResumeDelayMinutes { get; set; }
}

namespace IgorZ.TimberCommons.Common {
  public interface IConsumptionRateFormatter {
    string GetRate();
    string GetTime();
  }
}

namespace IgorZ.TimberDev.UI {
  public static class UnitFormats {
    public static string FormatDays(string value, Timberborn.Localization.ILoc loc) {
      return value;
    }
  }
}

namespace Timberborn.Localization {
  public interface ILoc {
    string T(string key, params object[] args);
  }
}

namespace TimberCommons.Tests {
  sealed class FakeLoc : Timberborn.Localization.ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}

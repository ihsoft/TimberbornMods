namespace SmartPower.Tests;

sealed class FakeLoc : Timberborn.Localization.ILoc {
  public string T(string key, params object[] args) {
    return key;
  }
}

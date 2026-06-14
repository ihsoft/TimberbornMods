namespace Timberborn.Localization;

public interface ILoc {
  string T(string key, params object[] args);
}

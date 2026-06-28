// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System.Globalization;
using System.Text;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents;

static class SignalNameSegment {
  const char EscapeMarker = 'X';

  /// <summary>
  /// Converts an external game/mod identifier into a single script-safe signal-name segment.
  /// </summary>
  /// <remarks>
  /// Segments that already match the scripting symbol rules are returned unchanged. Unsafe characters, a leading
  /// non-letter, and the escape marker itself are encoded as hexadecimal escape sequences prefixed with <c>X</c>.
  /// </remarks>
  public static string Encode(string value) {
    if (IsPlainSegment(value)) {
      return value;
    }

    var sb = new StringBuilder();
    for (var i = 0; i < value.Length; i++) {
      var symbol = value[i];
      if (IsSafeSymbol(symbol) && symbol != EscapeMarker && (i > 0 || IsLetter(symbol))) {
        sb.Append(symbol);
      } else if (symbol <= byte.MaxValue) {
        sb.Append(EscapeMarker);
        sb.Append(((int)symbol).ToString("X2", CultureInfo.InvariantCulture));
      } else {
        sb.Append(EscapeMarker);
        sb.Append('U');
        sb.Append(((int)symbol).ToString("X4", CultureInfo.InvariantCulture));
      }
    }
    return sb.ToString();
  }

  /// <summary>
  /// Decodes a signal-name segment produced by <see cref="Encode"/> back into the original external identifier.
  /// </summary>
  /// <returns>
  /// <c>true</c> when all escape sequences are well-formed; otherwise <c>false</c> and <paramref name="decodedValue"/>
  /// is set to <c>null</c>.
  /// </returns>
  public static bool TryDecode(string value, out string decodedValue) {
    var sb = new StringBuilder();
    for (var i = 0; i < value.Length; i++) {
      var symbol = value[i];
      if (symbol != EscapeMarker) {
        sb.Append(symbol);
        continue;
      }

      if (i + 1 >= value.Length) {
        decodedValue = null;
        return false;
      }
      if (value[i + 1] == 'U') {
        if (!TryParseHex(value, i + 2, 4, out var unicodeValue)) {
          decodedValue = null;
          return false;
        }
        sb.Append((char)unicodeValue);
        i += 5;
        continue;
      }
      if (!TryParseHex(value, i + 1, 2, out var asciiValue)) {
        decodedValue = null;
        return false;
      }
      sb.Append((char)asciiValue);
      i += 2;
    }
    decodedValue = sb.ToString();
    return true;
  }

  static bool IsPlainSegment(string value) {
    if (value.Length == 0 || !IsLetter(value[0])) {
      return false;
    }
    for (var i = 1; i < value.Length; i++) {
      if (!IsSafeSymbol(value[i])) {
        return false;
      }
    }
    return true;
  }

  static bool IsSafeSymbol(char symbol) {
    return IsLetter(symbol) || symbol is >= '0' and <= '9';
  }

  static bool IsLetter(char symbol) {
    return symbol is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
  }

  static bool TryParseHex(string value, int startIndex, int length, out int result) {
    result = 0;
    if (startIndex + length > value.Length) {
      return false;
    }
    for (var i = startIndex; i < startIndex + length; i++) {
      var digit = value[i] switch {
          >= '0' and <= '9' => value[i] - '0',
          >= 'A' and <= 'F' => value[i] - 'A' + 10,
          >= 'a' and <= 'f' => value[i] - 'a' + 10,
          _ => -1,
      };
      if (digit < 0) {
        return false;
      }
      result = result * 16 + digit;
    }
    return true;
  }
}

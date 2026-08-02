// Timberborn Mod: MapBrowser
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace IgorZ.MapBrowser;

static class SteamDescriptionFormatter {
  static readonly Regex TagRegex = new(@"\[(?<close>/)?(?<name>[a-z0-9*]+)(?:=(?<value>[^\]]+))?\]",
      RegexOptions.IgnoreCase | RegexOptions.Compiled);

  public static string Format(string description) {
    var formatter = new Formatter();
    var position = 0;
    foreach (Match match in TagRegex.Matches(description)) {
      formatter.AppendText(description[position..match.Index]);
      formatter.AppendTag(
          match.Groups["name"].Value.ToLowerInvariant(), match.Groups["close"].Success,
          match.Groups["value"].Success ? match.Groups["value"].Value : null, match.Value);
      position = match.Index + match.Length;
    }
    formatter.AppendText(description[position..]);
    return formatter.Finish();
  }

  sealed class Formatter {
    readonly StringBuilder _builder = new();
    readonly Stack<TagFrame> _frames = new();
    int _quoteDepth;
    int _suppressedDepth;
    bool _inCode;

    public void AppendText(string text) {
      if (_suppressedDepth > 0 || text.Length == 0) {
        return;
      }
      var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
      if (_quoteDepth > 0) {
        normalized = normalized.Replace("\n", "\n│ ");
      }
      _builder.Append(normalized.Replace('<', '＜').Replace('>', '＞'));
    }

    public void AppendTag(string name, bool closing, string value, string original) {
      if (_inCode && !(closing && name == "code")) {
        AppendText(original);
        return;
      }
      if (closing) {
        CloseTag(name);
        return;
      }
      if (_suppressedDepth > 0) {
        if (name is "img" or "previewyoutube" or "video") {
          _suppressedDepth++;
          _frames.Push(new TagFrame(name, string.Empty, TagKind.Suppressed));
        }
        return;
      }

      switch (name) {
        case "b":
          OpenInline(name, "<b>", "</b>");
          break;
        case "i":
          OpenInline(name, "<i>", "</i>");
          break;
        case "u":
        case "url":
          OpenInline(name, "<u>", "</u>");
          break;
        case "strike":
        case "s":
          OpenInline(name, "<s>", "</s>");
          break;
        case "h1":
          OpenBlock(name, "<size=140%><b>", "</b></size>");
          break;
        case "h2":
          OpenBlock(name, "<size=125%><b>", "</b></size>");
          break;
        case "h3":
          OpenBlock(name, "<size=110%><b>", "</b></size>");
          break;
        case "list":
        case "olist":
          EnsureNewLine();
          _frames.Push(new TagFrame(name, string.Empty, TagKind.List));
          break;
        case "*":
          AppendListMarker();
          break;
        case "quote":
          EnsureNewLine();
          _builder.Append("│ ");
          _quoteDepth++;
          _frames.Push(new TagFrame(name, string.Empty, TagKind.Quote));
          break;
        case "code":
          EnsureNewLine();
          _inCode = true;
          _frames.Push(new TagFrame(name, string.Empty, TagKind.Code));
          break;
        case "img":
        case "previewyoutube":
        case "video":
          _suppressedDepth++;
          _frames.Push(new TagFrame(name, string.Empty, TagKind.Suppressed));
          break;
        case "br":
          _builder.Append('\n');
          break;
        case "hr":
          EnsureNewLine();
          _builder.Append("────────");
          EnsureNewLine();
          break;
        case "p":
          EnsureParagraphBreak();
          break;
        case "tr":
          EnsureNewLine();
          break;
        case "td":
        case "th":
          if (_builder.Length > 0 && _builder[^1] != '\n') {
            _builder.Append("  ");
          }
          break;
      }
    }

    public string Finish() {
      while (_frames.Count > 0) {
        CloseFrame(_frames.Pop());
      }
      return Regex.Replace(_builder.ToString(), @"\n{3,}", "\n\n").Trim();
    }

    void OpenInline(string name, string opening, string closing) {
      _builder.Append(opening);
      _frames.Push(new TagFrame(name, closing, TagKind.Inline));
    }

    void OpenBlock(string name, string opening, string closing) {
      EnsureNewLine();
      _builder.Append(opening);
      _frames.Push(new TagFrame(name, closing, TagKind.Block));
    }

    void CloseTag(string name) {
      if (_frames.Count == 0 || _frames.Peek().Name != name) {
        return;
      }
      CloseFrame(_frames.Pop());
    }

    void CloseFrame(TagFrame frame) {
      switch (frame.Kind) {
        case TagKind.Inline:
          _builder.Append(frame.ClosingMarkup);
          break;
        case TagKind.Block:
          _builder.Append(frame.ClosingMarkup);
          EnsureNewLine();
          break;
        case TagKind.List:
          EnsureNewLine();
          break;
        case TagKind.Quote:
          _quoteDepth--;
          EnsureNewLine();
          break;
        case TagKind.Code:
          _inCode = false;
          EnsureNewLine();
          break;
        case TagKind.Suppressed:
          _suppressedDepth--;
          break;
      }
    }

    void AppendListMarker() {
      var list = FindList();
      if (list == null) {
        _builder.Append("• ");
        return;
      }
      EnsureNewLine();
      list.ItemNumber++;
      _builder.Append(list.Name == "olist" ? $"{list.ItemNumber}. " : "• ");
    }

    TagFrame FindList() {
      foreach (var frame in _frames) {
        if (frame.Kind == TagKind.List) {
          return frame;
        }
      }
      return null;
    }

    void EnsureNewLine() {
      if (_builder.Length > 0 && _builder[^1] != '\n') {
        _builder.Append('\n');
      }
    }

    void EnsureParagraphBreak() {
      EnsureNewLine();
      if (_builder.Length > 0 && (_builder.Length < 2 || _builder[^2] != '\n')) {
        _builder.Append('\n');
      }
    }
  }

  sealed class TagFrame(string name, string closingMarkup, TagKind kind) {
    public string Name { get; } = name;
    public string ClosingMarkup { get; } = closingMarkup;
    public TagKind Kind { get; } = kind;
    public int ItemNumber { get; set; }
  }

  enum TagKind {
    Inline,
    Block,
    List,
    Quote,
    Code,
    Suppressed,
  }
}

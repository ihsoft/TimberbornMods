using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.UIElements;

namespace IgorZ.TimberUI.Core;

sealed record VisualElementRecord {

  const string DocumentRootName = "UnityEngine.UIElements.UXML";

  static readonly Dictionary<string, string> StandardStylesPaths = new() {
      { "CommonStyle", "project://database/Assets/Resources/UI/Views/Common/CommonStyle.uss" },
      { "CoreStyle", "project://database/Assets/Resources/UI/Views/Core/CoreStyle.uss" },
      { "EntityPanelGameStyle", "project://database/Assets/Resources/UI/Views/Game/EntityPanel/EntityPanelGameStyle.uss" },
      { "EntityPanelCommonStyle", "project://database/Assets/Resources/UI/Views/Common/EntityPanel/EntityPanelCommonStyle.uss" },
  };

  static readonly List<(string Prefix, string Alias)> TypesAliases = [
      ("UnityEngine.UIElements.", "engine"),
  ];

  public VisualTreeAsset TreeAsset { get; init; }
  public VisualElementAsset Asset { get; init; }
  public List<VisualElementRecord> Hierarchy { get; } = [];

  public void PrintTree(StringBuilder sb, int depth) {
    var parentIndentation = new string(' ', depth * 4);
    sb.Append($"{parentIndentation}<{GetReducedTypeName(Asset)}");
    if (Asset.fullTypeName == DocumentRootName) {
      foreach (var alias in TypesAliases) {
        sb.Append($" xmlns:{alias.Alias}=\"{alias.Prefix[..^1]}\"");
      }
    }
    if (Asset is TemplateAsset templateAsset) {
      sb.Append($" template=\"{templateAsset.templateAlias}\"");
    }
    if (Asset.TryGetAttributeValue("text", out var text)) {
      sb.Append($" text=\"{text}\"");
    }
    if (Asset.TryGetAttributeValue("name", out var name)) {
      sb.Append($" name=\"{name}\"");
    }
    if (Asset.classes.Length > 0) {
      sb.Append($" class=\"{string.Join(" ", Asset.classes)}\"");
    }
    if (Asset.TryGetAttributeValue("style", out var style)) {
      sb.Append($" style=\"{style}\"");
    }
    if (Hierarchy.Count == 0 && Asset.stylesheets.Count == 0 && Asset.stylesheetPaths.Count == 0) {
      sb.AppendLine(" />");
      return;
    }
    sb.AppendLine(">");
    var childIndentation = new string(' ', (depth + 1) * 4);
    if (TreeAsset != null) {
      var templates = TreeAsset.templateAssets.Select(x => x.templateAlias).Distinct().OrderBy(x => x).ToList();
      var engineAlias = TypesAliases.First(x => x.Prefix == "UnityEngine.UIElements.").Alias;
      foreach (var template in templates) {
        sb.AppendLine($"{childIndentation}<{engineAlias}:Template name=\"{template}\" src=\"{template}.uxml\" />");
      }
    }
    foreach (var styleSheet in Asset.stylesheets) {
      var stylesheetPath =
          StandardStylesPaths.TryGetValue(styleSheet.name, out var path) ? path : styleSheet.name + ".uss";
      sb.AppendLine($"{childIndentation}<Style src=\"{stylesheetPath}\" />");
    }
    foreach (var stylesheetPath in Asset.stylesheetPaths) {
      sb.AppendLine($"{childIndentation}<Style src=\"{stylesheetPath}\" />");
    }
    foreach (var child in Hierarchy) {
      child.PrintTree(sb, depth + 1);
    }
    sb.AppendLine($"{parentIndentation}</{GetReducedTypeName(Asset)}>");
  }

  static string GetReducedTypeName(VisualElementAsset element) {
    var fullTypeName = element.fullTypeName;
    foreach (var (prefix, alias) in TypesAliases) {
      if (fullTypeName.StartsWith(prefix)) {
        return alias + ":" + fullTypeName[prefix.Length..];
      }
    }
    return fullTypeName;
  }
}

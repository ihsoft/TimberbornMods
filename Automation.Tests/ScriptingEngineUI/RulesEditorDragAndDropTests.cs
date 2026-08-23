using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Automation.Tests;

static class RulesEditorDragAndDropTests {
  public static void UsesRuntimeReorderingWithoutVisibleHandle() {
    var document = XDocument.Load(GetAssetPath("RulesEditor.uxml"));
    var list = document.Descendants().Single(
        element => element.Name.LocalName == "ListView" && (string)element.Attribute("name") == "RuleRowsList");

    Assert.Equal("true", (string)list.Attribute("reorderable"));
    Assert.Equal("Animated", (string)list.Attribute("reorder-mode"));
    Assert.Equal("DynamicHeight", (string)list.Attribute("virtualization-method"));
    Assert.True(document.Descendants().Any(
        element => element.Name.LocalName == "ScrollView" && (string)element.Attribute("name") == "RuleRowsPreview"));

    var compactStyle = Regex.Replace(File.ReadAllText(GetAssetPath("RulesEditorStyle.uss")), @"\s+", "");
    Assert.True(compactStyle.Contains(".rules-editor-list.unity-list-view__reorderable-handle{display:none;}"));
  }

  static string GetAssetPath(string assetName) {
    return Path.Combine(AppContext.BaseDirectory, "TestAssets", assetName);
  }
}

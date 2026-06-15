using System;
using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace TimberDev.Tests;

static class VisualElementExtensionsTests {
  public static void FindsElementOrReportsMissingElement() {
    var root = new VisualElement();
    var child = new VisualElement { name = "child" };
    root.Add(child);

    Assert.Equal(child, root.Q2<VisualElement>("child"));
    Assert.Equal(null, root.Q2<VisualElement>("missing", throwIfNotFound: false));
    Assert.Throws<InvalidOperationException>(() => root.Q2<VisualElement>("missing"));
  }
}

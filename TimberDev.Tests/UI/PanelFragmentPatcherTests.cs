using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace TimberDev.Tests;

static class PanelFragmentPatcherTests {
  public static void InsertsElementAfterTargetOnlyOnce() {
    var root = new VisualElement();
    var fragmentPanelRoot = new VisualElement();
    var fragment = new VisualElement { name = PanelFragmentPatcher.MechanicalNodeFragmentName };
    var first = new VisualElement { name = "First" };
    var target = new VisualElement { name = "Target" };
    var inserted = new VisualElement();
    root.Add(fragmentPanelRoot);
    root.Add(fragment);
    fragment.Add(first);
    fragment.Add(target);

    var patcher = new PanelFragmentPatcher(
        inserted, fragmentPanelRoot, PanelFragmentPatcher.MechanicalNodeFragmentName, "Target");

    patcher.Patch();
    patcher.Patch();

    Assert.Equal(3, fragment.childCount);
    Assert.Equal(inserted, fragment.ChildAt(2));
  }

  public static void AppendsElementWhenTargetIsMissing() {
    var root = new VisualElement();
    var fragmentPanelRoot = new VisualElement();
    var fragment = new VisualElement { name = PanelFragmentPatcher.MechanicalNodeFragmentName };
    var inserted = new VisualElement();
    root.Add(fragmentPanelRoot);
    root.Add(fragment);

    new PanelFragmentPatcher(
        inserted, fragmentPanelRoot, PanelFragmentPatcher.MechanicalNodeFragmentName, "Missing").Patch();

    Assert.Equal(1, fragment.childCount);
    Assert.Equal(inserted, fragment.ChildAt(0));
  }
}

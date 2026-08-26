using System.Reflection;
using IgorZ.TimberCommons.ModUIPatches;
using Timberborn.Modding;
using Timberborn.ModdingUI;
using UnityEngine.UIElements;

namespace TimberCommons.Tests;

static class ModListViewInitializePatchTests {
  public static void FiltersInactiveModsAndRestoresFullList() {
    var (modListView, root, activeRoot, inactiveRoot) = CreateView();
    InvokePostfix(modListView, root);
    var filterButton = root.Q<Button>("ShowActiveModsButton");

    Assert.Equal(
        "IgorZ.TimberCommons.ModUIPatches.ModListView.ShowActiveMods:1,2",
        filterButton.text);

    filterButton.Click();

    Assert.Equal(DisplayStyle.Flex, activeRoot.style.display);
    Assert.Equal(DisplayStyle.None, inactiveRoot.style.display);
    Assert.Equal("IgorZ.TimberCommons.ModUIPatches.ModListView.ShowAllMods:2", filterButton.text);

    filterButton.Click();

    Assert.Equal(DisplayStyle.Flex, activeRoot.style.display);
    Assert.Equal(DisplayStyle.Flex, inactiveRoot.style.display);
  }

  public static void AddsFilterButtonOnlyOnce() {
    var (modListView, root, _, _) = CreateView();

    InvokePostfix(modListView, root);
    var firstButton = root.Q<Button>("ShowActiveModsButton");
    InvokePostfix(modListView, root);

    Assert.Same(firstButton, root.Q<Button>("ShowActiveModsButton"));
  }

  static (ModListView View, VisualElement Root, VisualElement ActiveRoot, VisualElement InactiveRoot) CreateView() {
    ModPlayerPrefsHelper.Reset();
    ModListViewInitializePatch.SetLoc(new FakeLoc());
    var root = new VisualElement();
    var buttonContainer = new VisualElement();
    buttonContainer.Add(new Button { name = "ResetOrderButton" });
    root.Add(buttonContainer);

    var activeMod = new Mod();
    var inactiveMod = new Mod();
    ModPlayerPrefsHelper.SetEnabled(activeMod, true);
    var activeRoot = new VisualElement();
    var inactiveRoot = new VisualElement();
    var modListView = new ModListView();
    modListView._modItems.Add(activeMod, new ModItem(activeRoot));
    modListView._modItems.Add(inactiveMod, new ModItem(inactiveRoot));
    return (modListView, root, activeRoot, inactiveRoot);
  }

  static void InvokePostfix(ModListView modListView, VisualElement root) {
    typeof(ModListViewInitializePatch)
        .GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, [modListView, root]);
  }
}

using System.Reflection;
using IgorZ.TimberDev.UI;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using UnityEngine.UIElements;

namespace TimberDev.Tests;

static class AbstractDialogTests {
  public static void ShowWiresButtonsAndConfirmAppliesValidInput() {
    var dialog = CreateDialog(out var panelStack, out _);

    dialog.Show();
    dialog.GetPanel().Q<Button>("ConfirmButton").Click();

    Assert.Equal(1, panelStack.Pushed.Count);
    Assert.Equal(1, dialog.ApplyCount);
    Assert.Equal(1, panelStack.Popped.Count);
    Assert.Equal(null, dialog.GetPanel());
  }

  public static void ConfirmShowsErrorAndKeepsDialogOpen() {
    var dialog = CreateDialog(out var panelStack, out var dialogBoxShower);
    dialog.VerifyError = "Bad input";

    dialog.Show();
    dialog.GetPanel().Q<Button>("ConfirmButton").Click();

    Assert.Equal(0, dialog.ApplyCount);
    Assert.Equal(0, panelStack.Popped.Count);
    Assert.Equal("Bad input", dialogBoxShower.LastDialogBox.Message);
    Assert.True(dialogBoxShower.LastDialogBox.Shown);
    Assert.True(dialog.GetPanel() != null);
  }

  public static void CancelClosesOrConfirmsUnsavedChanges() {
    var dialog = CreateDialog(out var panelStack, out var dialogBoxShower);

    dialog.Show();
    dialog.GetPanel().Q<Button>("CancelButton").Click();
    Assert.Equal(1, panelStack.Popped.Count);

    dialog.Show();
    dialog.HasChanges = true;
    dialog.OnUICancelled();
    Assert.Equal(1, panelStack.Popped.Count);
    Assert.True(dialogBoxShower.LastDialogBox.Shown);

    dialogBoxShower.LastDialogBox.ConfirmAction();

    Assert.Equal(2, panelStack.Popped.Count);
    Assert.Equal(null, dialog.GetPanel());
  }

  static TestDialog CreateDialog(out PanelStack panelStack, out DialogBoxShower dialogBoxShower) {
    var uiFactory = CreateFactory();
    panelStack = new PanelStack();
    dialogBoxShower = new DialogBoxShower();
    var dialog = new TestDialog();
    dialog.InjectDependencies(uiFactory, panelStack, dialogBoxShower);
    return dialog;
  }

  static UiFactory CreateFactory() {
    var constructor = typeof(UiFactory).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [
            typeof(VisualElementLoader),
            typeof(Timberborn.Localization.ILoc),
            typeof(IAssetLoader),
            typeof(VisualElementInitializer),
            typeof(DropdownListDrawer),
        ],
        null);
    return (UiFactory)constructor.Invoke([
        new VisualElementLoader(),
        new FakeLoc(),
        new TestAssetLoader(),
        new VisualElementInitializer(),
        new DropdownListDrawer(),
    ]);
  }

  sealed class TestDialog : AbstractDialog {
    public string VerifyError { get; set; }
    public bool HasChanges { get; set; }
    public int ApplyCount { get; private set; }

    protected override string DialogResourceName => "TestDialog";

    protected override string VerifyInput() {
      return VerifyError;
    }

    protected override void ApplyInput() {
      ApplyCount++;
    }

    protected override bool CheckHasChanges() {
      return HasChanges;
    }
  }
}

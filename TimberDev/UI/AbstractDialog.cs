// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using Bindito.Core;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>Base class for all dialogs. The descendants must be created via injection.</summary>
/// <remarks>Copy the localization strings from the loc files in UI directory into the main mod loc file.</remarks>
public abstract class AbstractDialog : IPanelController {

  const string UnsavedChangesConfirmationLocKey = "TimberDev_UI.AbstractDialog.UnsavedChangesConfirmation";

  #region API

  /// <summary>Shows this dialog.</summary>
  public virtual void Show() {
    Root = UiFactory.LoadVisualTreeAsset(DialogResourceName);
    Root.Q2<Button>(ConfirmButtonName).clicked += () => OnUIConfirmed();
    if (CancelButtonName != null) {
      Root.Q2<Button>(CancelButtonName).clicked += Close;
    }
    var closeElement = Root.Q<Button>(CloseElementName);
    if (closeElement != null) {
      closeElement.clicked += Close;
    }
    _panelStack.PushDialog(this);
  }

  /// <summary>Closes this dialog without saving any data.</summary>
  public virtual void Close() {
    _panelStack.Pop(this);
    Root = null;
  }

  /// <summary>
  /// The name of the UXML resource that defines this dialog. Relative to "UI/Views" folder.
  /// </summary>
  protected abstract string DialogResourceName { get; }

  /// <summary>Name of the "Cancel" button element in the UXML.</summary>
  /// <remarks>Can be set to <c>null</c> to skip binding.</remarks>
  protected virtual string CancelButtonName => "CancelButton";

  /// <summary>Name of the "Confirm" button element in the UXML.</summary>
  protected virtual string ConfirmButtonName => "ConfirmButton";

  /// <summary>Name of the element that closes the dialog without saving and confirmation.</summary>
  /// <remarks>
  /// This element is optional. If it's not found in the asset, it doesn't trigger an error. It's usually a cross icon
  /// on the common dialog frame.
  /// </remarks>
  protected virtual string CloseElementName => "CloseButton";

  /// <summary>Root element of this dialog.</summary>
  /// <remarks>It is populated on dialog show and gets destroyed on dialog close.</remarks>
  protected VisualElement Root { get; private set; }

  /// <summary>Verifies the input data. Called when the user clicks "OK".</summary>
  /// <returns>
  /// <c>null</c> if the input value is valid and the dialog can be commited and closed. Otherwise, a localized string
  /// that will be shown to the user as an error message. And the dialog will stay open.
  /// </returns>
  /// <seealso cref="ApplyInput"/>
  /// <seealso cref="UiFactory"/>
  protected abstract string VerifyInput();

  /// <summary>Applies the input data. Called when the user clicks "OK" and the input is valid.</summary>
  /// <remarks>Called after <see cref="VerifyInput"/>.</remarks>
  protected abstract void ApplyInput();

  /// <summary>Checks if the user has made any changes to the input data.</summary>
  /// <returns><c>true</c> if there are unsaved changes.</returns>
  /// <seealso cref="OnUICancelled"/>
  protected abstract bool CheckHasChanges();

  /// <summary>UI factory utility class. Use it to manipulate the elements.</summary>
  protected UiFactory UiFactory { get; private set; }

  #endregion

  #region IPanelController implementation

  /// <inheritdoc/>
  public VisualElement GetPanel() {
    return Root;
  }

  /// <inheritdoc/>
  public bool OnUIConfirmed() {
    var maybeError = VerifyInput();
    if (maybeError != null) {
      _dialogBoxShower.Create().SetMessage(maybeError).Show();
      return true;
    }
    ApplyInput();
    Close();
    return false;
  }

  /// <inheritdoc/>
  public void OnUICancelled() {
    if (CheckHasChanges()) {
      _dialogBoxShower.Create()
          .SetMessage(UiFactory.T(UnsavedChangesConfirmationLocKey))
          .SetConfirmButton(Close)
          .SetCancelButton(() => {})
          .Show();
      return;
    }
    Close();
  }

  #endregion

  #region Implementation

  PanelStack _panelStack;
  DialogBoxShower _dialogBoxShower;

  /// <summary>Public for the inject to work properly.</summary>
  [Inject]
  public void InjectDependencies(UiFactory uiFactory, PanelStack panelStack, DialogBoxShower dialogBoxShower) {
    UiFactory = uiFactory;
    _panelStack = panelStack;
    _dialogBoxShower = dialogBoxShower;
  }

  #endregion
}

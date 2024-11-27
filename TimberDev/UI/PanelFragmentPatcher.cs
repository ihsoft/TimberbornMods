// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using UnityDev.Utils.LogUtilsLite;
using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace IgorZ.TimberDev.UI;

/// <summary>Utility class to patch the stock UI panel fragments with custom elements.</summary>
public sealed class PanelFragmentPatcher {

  /// <summary>Fragment name for the mechanical node (generator/consumer/network).</summary>
  public const string MechanicalNodeFragmentName = "MechanicalNodeFragment";

  readonly VisualElement _element;
  readonly VisualElement _fragmentPanelRoot;
  readonly string _fragmentName;
  readonly string _afterElementName;
  readonly int _offset;
  bool _isPatched;

  /// <summary>Creates a patch of the UI fragment with the element.</summary>
  /// <remarks>Call <see cref="Patch"/> method from the fragment show method to actually add the element.</remarks>
  /// <param name="element">The element to add.</param>
  /// <param name="fragmentPanelRoot">Root of a panel fragment.</param>
  /// <param name="fragmentName">
  /// The stock fragment name to attach the element to. For example <see cref="MechanicalNodeFragmentName"/>.
  /// </param>
  /// <param name="afterElementName">
  /// The optional name of the stock element to add the custom element after. If set to empty string, then the custom
  /// element will be inserted at the top, before any other stock elements.
  /// </param>
  /// <param name="offset">Offset, relative to the <paramref name="afterElementName"/>.</param>
  public PanelFragmentPatcher(VisualElement element, VisualElement fragmentPanelRoot, string fragmentName,
                              string afterElementName = null, int offset = 0) {
    _element = element;
    _fragmentPanelRoot = fragmentPanelRoot;
    _fragmentName = fragmentName;
    _offset = offset;
    _afterElementName = afterElementName;
  }

  /// <summary>Patches the UI fragment with the element. Actually does the patch the first call only.</summary>
  public void Patch() {
    if (_isPatched) {
      return;
    }
    _isPatched = true;

    var fragment = _fragmentPanelRoot.parent.Q<VisualElement>(_fragmentName);
    if (fragment == null) {
      ReportUiPatchingError($"Cannot find {_fragmentName} fragment in root");
      return;
    }
    if (_afterElementName == "") {
      fragment.Insert(0, _element);
    } else if (_afterElementName == null) {
      fragment.Add(_element);
    } else {
      var afterElement = fragment.IndexOf(fragment.Q<VisualElement>(_afterElementName));
      if (afterElement == -1) {
        ReportUiPatchingError($"Cannot find {_afterElementName} element in {_fragmentName} fragment");
        afterElement = fragment.childCount;
      }
      fragment.Insert(afterElement + 1 + _offset, _element);
    }
    DebugEx.Fine("Patched the UI fragment '{0}' with {1} after '{2}'", _fragmentName, _element, _afterElementName);
  }

  static void ReportUiPatchingError(string context) {
    DebugEx.Error("Cannot patch the stock UI! Report this error to the mod owner. Context:\n{1}", context);
  }
}

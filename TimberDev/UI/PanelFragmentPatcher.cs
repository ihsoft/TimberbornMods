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
  /// The name of the stock element to add the custom element after. If the specified element is not found, then the
  /// custom element will be added to the fragment panel root at the bottom children list.
  /// </param>
  /// <param name="offset">Offset, relative to the <paramref name="afterElementName"/>.</param>
  public PanelFragmentPatcher(VisualElement element, VisualElement fragmentPanelRoot, string fragmentName,
                              string afterElementName, int offset = 0) {
    _element = element;
    _fragmentPanelRoot = fragmentPanelRoot;
    _fragmentName = fragmentName;
    _offset = offset;
    _afterElementName = afterElementName;
  }

  /// <summary>Patches the UI fragment with the element. Actually does the patch in the first call only.</summary>
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
    var afterElement = fragment.Q(_afterElementName);
    if (afterElement == null) {
      ReportUiPatchingError($"Cannot find {_afterElementName} element in {_fragmentName} fragment");
      fragment.Add(_element);
      return;
    }
    var container = afterElement.parent;
    var pos = container.IndexOf(afterElement) + 1 + _offset;
    if (pos > container.childCount) {
      ReportUiPatchingError($"Cannot add after {_afterElementName}+{_offset}: {pos} > {container.childCount}");
      pos -= _offset;
    }
    container.Insert(pos, _element);
    DebugEx.Fine("Patched UI fragment '{0}' with {1} after '{2}'+{3}",
                 _fragmentName, _element.GetType().Name, _afterElementName, _offset);
  }

  static void ReportUiPatchingError(string context) {
    DebugEx.Error("Cannot patch the stock UI! Report this error to the mod owner. Context:\n{0}", context);
  }
}

// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace IgorZ.TimberDev.UI;

/// <summary>Factory for creating UI elements without using TAPI.</summary>
/// <remarks>Normally, should be used for the times when TAPI is broken.</remarks>
public class UiFactory {
  readonly VisualElementLoader _visualElementLoader;
  readonly ILoc _loc;
  readonly IAssetLoader _assetLoader;

  /// <summary>Common padding around the button text on the right side panel.</summary>
  public static readonly (int top, int left, int bottom, int right) StandardButtonPadding = new(2, 10, 2, 10);

  /// <summary>Default color for the text in UI.</summary>
  public static readonly Color DefaultColor = new(0.8f, 0.8f, 0.8f);

  /// <summary>Find the specified element starting from the parent and upstream.</summary>
  public static T FindElementUpstream<T>(VisualElement root, string name) where T : VisualElement {
    while (root != null) {
      var element = root.Q<T>(name);
      if (element != null) {
        return element;
      }
      root = root.parent;
    }
    throw new InvalidOperationException($"Element '{name}' not found.");
  }

  UiFactory(VisualElementLoader visualElementLoader, ILoc loc, IAssetLoader assetLoader) {
    _visualElementLoader = visualElementLoader;
    _loc = loc;
    _assetLoader = assetLoader;
  }

  /// <summary>A shortcut to "ILoc.T()".</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public string T(string key) => _loc.T(key);

  /// <summary>A shortcut to "ILoc.T()".</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public string T<T1>(string key, T1 param1) => _loc.T(key, param1);

  /// <summary>A shortcut to "ILoc.T()".</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public string T<T1, T2>(string key, T1 param1, T2 param2) => _loc.T(key, param1, param2);

  /// <summary>A shortcut to "ILoc.T()".</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public string T<T1, T2, T3>(string key, T1 param1, T2 param2, T3 param3) => _loc.T(key, param1, param2, param3);

  /// <summary>Stylesheet for the Timberborn UI.</summary>
  public StyleSheet TimberbornStylesheet =>
      _timberbornStylesheet ??= _assetLoader.Load<StyleSheet>("UI/Views/TimberbornStyle");
  StyleSheet _timberbornStylesheet;

  /// <summary>Creates a panel builder that can be used as a fragment on the right side panel.</summary>
  public VisualElement CreateCenteredPanelFragment() {
    var root = _visualElementLoader.LoadVisualElement("Game/EntityPanel/BatteryFragment");
    root.Clear();
    return root;
  }

  /// <summary>Wraps the element to make it centered in the UI fragment.</summary>
  public VisualElement CenterElement(VisualElement element) {
    var center = new VisualElement();
    center.style.justifyContent = Justify.Center;
    center.style.flexDirection = FlexDirection.Row;
    center.Add(element);
    return center;
  }

  /// <summary>Creates a label in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Optional loc key for the caption.</param>
  public Label CreateLabel(string locKey = null) {
    var label = _visualElementLoader.LoadVisualElement("Game/EntityPanel/SluiceFragment").Q<Label>("DepthLabel");
    label.RemoveFromHierarchy();
    if (locKey != null) {
      label.text = T(locKey);
    }
    return label;
  }

  /// <summary>Creates a button in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Loc key for the caption.</param>
  /// <param name="onClickFn">Callback to call when the button is clicked.</param>
  /// <param name="padding">
  /// Optional padding around the button text. If not set, then <see cref="StandardButtonPadding"/> will be used.
  /// </param>
  public Button CreateButton(string locKey, Action onClickFn,
                             (int top, int left, int bottom, int right)? padding = null) {
    var button = _visualElementLoader.LoadVisualElement("Game/EntityPanel/DebugFragment").Q<Button>("Button");
    button.RemoveFromHierarchy();
    button.text = T(locKey);
    button.style.marginTop = 0;
    button.style.fontSize = 14;
    button.style.color = Color.white;
    var setPadding = padding ?? StandardButtonPadding;
    button.style.paddingTop = setPadding.top;
    button.style.paddingLeft = setPadding.left;
    button.style.paddingBottom = setPadding.bottom;
    button.style.paddingRight = setPadding.right;
    button.clicked += onClickFn;
    return button;
  }

  /// <summary>Creates a toggle in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Loc key for the caption. If null or empty, then there will be no caption.</param>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  public Toggle CreateToggle(string locKey, Action<ChangeEvent<bool>> onValueChangedFn, int spacing = 5) {
    var toggle = _visualElementLoader.LoadVisualElement("Game/EntityPanel/HaulCandidateFragment").Q<Toggle>("Toggle");
    toggle.RemoveFromHierarchy();
    if (locKey != null) {
      toggle.text = T(locKey);
    }
    toggle.RegisterValueChangedCallback(evt => onValueChangedFn(evt));
    if (spacing >= 0) {
      toggle.style.marginBottom = spacing;
    }
    return toggle;
  }

  /// <summary>Creates a slider in a theme suitable for the right side panel.</summary>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="stepSize">
  /// The minimum delta for the value changes. All positions on the slider will be multiples of this value.
  /// </param>
  /// <param name="lowValue">The lowest possible value.</param>
  /// <param name="highValue">The highest possible value.</param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  public Slider CreateSlider(Action<ChangeEvent<float>> onValueChangedFn, float lowValue, float highValue,
                             float stepSize = 0, int spacing = 5) {
    var slider = _visualElementLoader.LoadVisualElement("Game/EntityPanel/BatteryFragment").Q<Slider>("ChargeSlider");
    slider.RemoveFromHierarchy();
    slider.style.marginTop = 0;
    slider.lowValue = lowValue;
    slider.highValue = highValue;
    slider.RegisterValueChangedCallback(
        evt => {
          if (stepSize > 0) {
            var newValue = Mathf.Round(slider.value / stepSize) * stepSize;
            slider.SetValueWithoutNotify(newValue);
            evt = ChangeEvent<float>.GetPooled(evt.previousValue, newValue);
          }
          onValueChangedFn(evt);
        });
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    return slider;
  }

  /// <summary>Creates a min/max slider in a theme suitable for the right side panel.</summary>
  /// <remarks>
  /// For this method to work, "TimberbornStyle.uss" should be compiled into the mod asset bundle at path
  /// "UI/Views/TimberbornStyle".
  /// </remarks>
  /// <param name="onValueChangedFn">A callback method that will be called on the value change.</param>
  /// <param name="lowValue">The minimum value limit.</param>
  /// <param name="highValue">The maximum value limit.</param>
  /// <param name="minDelta">The minimum delta between min/max values.</param>
  /// <param name="stepSize">If greater than zero, then the values are rounded to the step.</param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  public MinMaxSlider CreateMinMaxSlider(Action<ChangeEvent<Vector2>> onValueChangedFn, float lowValue, float highValue,
                                          float minDelta, float stepSize = 0, int spacing = 5) {
    var slider = new MinMaxSlider(lowValue, highValue, lowValue, highValue);
    slider.styleSheets.Add(TimberbornStylesheet);
    slider.RegisterValueChangedCallback(
        evt => {
          var newValue = evt.newValue;
          if (stepSize > 0) {
            newValue = new Vector2(
                Mathf.Round(evt.newValue.x / stepSize) * stepSize, Mathf.Round(evt.newValue.y / stepSize) * stepSize);
          }
          if (newValue.y - newValue.x < minDelta) {
            if (Math.Abs(evt.previousValue.x - newValue.x) < float.Epsilon) {
              newValue.y = newValue.x + minDelta;
            } else {
              newValue.x = newValue.y - minDelta;
            }
          }
          slider.SetValueWithoutNotify(newValue);
          evt = ChangeEvent<Vector2>.GetPooled(evt.previousValue, newValue);
          onValueChangedFn(evt);
        });
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    return slider;
  }

  /// <summary>
  /// Creates a slider with plus/minus buttons and discrete value changes in a theme suitable for the right side panel.
  /// </summary>
  /// <param name="stepSize">
  /// The minimum delta for the value changes. All positions on the slider will be multiples of this value.
  /// </param>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  /// <returns>A wrapper for the precise slider.</returns>
  public PreciseSliderWrapper CreatePreciseSlider(float stepSize, Action<float> onValueChangedFn, int spacing = 5) {
    var root = _visualElementLoader.LoadVisualElement("Game/EntityPanel/SluiceFragment");
    var slider = root.Q<PreciseSlider>("WaterLevelSlider");
    slider.RemoveFromHierarchy();
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    return new PreciseSliderWrapper(slider, onValueChangedFn, stepSize);
  }
}

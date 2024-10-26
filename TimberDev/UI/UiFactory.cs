// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using TimberApi.UIBuilderSystem;
using TimberApi.UIBuilderSystem.StylingElements;
using TimberApi.UIPresets.Buttons;
using TimberApi.UIPresets.Labels;
using TimberApi.UIPresets.Sliders;
using TimberApi.UIPresets.Toggles;
using Timberborn.CoreUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.UI;

/// <summary>Factory for making standard fragment panel elements.</summary>
public sealed class UiFactory {
  readonly VisualElementLoader _visualElementLoader;

  /// <summary>Common padding around the button text on the right side panel.</summary>
  public static readonly Padding StandardButtonPadding = new(2, 10, 2, 10);

  /// <summary>The TAPI UI builder.</summary>
  public readonly UIBuilder UiBuilder;

  /// <summary>The localization service to use for the UI elements.</summary>
  public readonly ILoc Loc;

  UiFactory(VisualElementLoader visualElementLoader, UIBuilder uiBuilder, ILoc loc) {
    _visualElementLoader = visualElementLoader;
    UiBuilder = uiBuilder;
    Loc = loc;
  }

  /// <summary>Creates a slider in a theme suitable for the right side panel.</summary>
  /// <remarks>
  /// TAPI offers the sliders builder, but in the recent updates it got broken. Use this factory as a quick workaround.
  /// </remarks>
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
    var slider = UiBuilder.Create<GameTextSlider>().Small().Build();
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
  /// <param name="onValueChangedFn">A callback method that will be called on the value change.</param>
  /// <param name="lowValue">The minimum value limit.</param>
  /// <param name="highValue">The maximum value limit.</param>
  /// <param name="minDelta">The minimum delta between min/max values.</param>
  /// <param name="stepSize">If greater than zero, then the values are rounded to the step.</param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  public MinMaxSlider CreateMinMaxSlider(Action<ChangeEvent<Vector2>> onValueChangedFn, float lowValue, float highValue,
                                         float minDelta, float stepSize = 0, int spacing = 5) {
    var slider = UiBuilder.Create<GameTextMinMaxSlider>()
        .SetLowLimit(lowValue)
        .SetHighLimit(highValue)
        .Small().Build();
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
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    return new PreciseSliderWrapper(slider, onValueChangedFn, stepSize);
  }

  /// <summary>Creates a toggle in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Loc key for the caption. If null or empty, then there will be no caption.</param>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  public Toggle CreateToggle(string locKey, Action<ChangeEvent<bool>> onValueChangedFn, int spacing = 5) {
    var toggle = !string.IsNullOrEmpty(locKey)
        ? UiBuilder.Create<GameToggle>().SetLocKey(locKey).Build()
        : UiBuilder.Create<GameTextToggle>().Build();
    toggle.RegisterValueChangedCallback(evt => onValueChangedFn(evt));
    if (spacing >= 0) {
      toggle.style.marginBottom = spacing;
    }
    return toggle;
  }

  /// <summary>Creates a label in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Optional loc key for the caption.</param>
  public Label CreateLabel(string locKey = null) {
    return UiBuilder.Create<GameTextLabel>().SetText(locKey != null ? Loc.T(locKey) : "").Build();
  }

  /// <summary>Creates a button in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Loc key for the игеещт caption.</param>
  /// <param name="onClickFn">Callback to call when the button is clicked.</param>
  /// <param name="padding">
  /// Optional padding around the button text. If not set, then <see cref="StandardButtonPadding"/> will be used.
  /// </param>
  public Button CreateButton(string locKey, Action onClickFn, Padding? padding = null) {
    var button = UiBuilder.Create<GameButton>()
        .SetLocKey(locKey)
        .ModifyRoot(builder => builder.SetPadding(padding ?? StandardButtonPadding))
        .Build();
    button.clicked += onClickFn;
    return button;
  }

  /// <summary>Wraps the element to make it centered in the UI fragment.</summary>
  public VisualElement CenterElement(VisualElement element) {
    var center = new VisualElement {
        style = {
            justifyContent = Justify.Center,
            flexDirection = FlexDirection.Row
        }
    };
    center.Add(element);
    return center;
  }

  /// <summary>Creates a panel builder that can be used as a fragment on the right side panel.</summary>
  /// <remarks>
  /// This is a root element for the fragment's panel. Add controls to it via
  /// <see cref="PanelFragmentBuilder{PanelFragment}.AddComponent(UnityEngine.UIElements.VisualElement)"/>.
  /// </remarks>
  public PanelFragment CreateCenteredPanelFragmentBuilder() {
    return UiBuilder.Create<PanelFragment>()
        .SetFlexDirection(FlexDirection.Column)
        .SetWidth(new Length(100f, LengthUnit.Percent))
        .SetJustifyContent(Justify.Center);
  }
}

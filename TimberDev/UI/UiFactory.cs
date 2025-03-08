// Timberborn Utils
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Runtime.CompilerServices;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.DropdownSystem;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

// ReSharper disable MemberCanBePrivate.Global
namespace IgorZ.TimberDev.UI;

/// <summary>Factory for creating UI elements without using TAPI.</summary>
/// <remarks>Normally, should be used for the times when TAPI is broken.</remarks>
public class UiFactory {

  /// <summary>Path to the UI views in the game assets.</summary>
  public const string UIViewsPath = "UI/Views/";

  /// <summary>Class name for the text elements on the right side panel.</summary>
  public const string EntityPanelTextClass = "entity-panel__text";

  /// <summary>Class name for the big text elements.</summary>
  public const string GameTextBigClass = "game-text-big";

  /// <summary>Class name for the small text elements.</summary>
  public const string GameTextSmallClass = "game-text-small";

  /// <summary>Class name for the normal text elements.</summary>
  public const string GameTextNormalClass = "game-text-small";

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

  /// <summary>Sets an alternative path to <see cref="TimberDevStylesheet"/>.</summary>
  /// <remarks>If the path wasn't set, then stylesheet will be loaded from "UI/Views/TimberDevStyle".</remarks>
  public string TimberDevStylesheetPath {
    get => _timberDevStylesheetPath;
    set {
      if (_timberDevStylesheetPath != value) {
        _timberDevStylesheetPath = value;
        _timberDevStylesheet = null;
      }
    }
  }
  string _timberDevStylesheetPath = "UI/Views/TimberDevStyle";

  /// <summary>Stylesheet for the TimberDev UI elements.</summary>
  /// <remarks>The stylesheet should be built-in to the mod asset file. See "README.txt".</remarks>
  public StyleSheet TimberDevStylesheet => _timberDevStylesheet
      ??= _assetLoader.Load<StyleSheet>(TimberDevStylesheetPath);
  StyleSheet _timberDevStylesheet;

  /// <summary>Creates a panel builder that can be used as a fragment on the right side panel.</summary>
  public VisualElement CreateCenteredPanelFragment() {
    var root = new NineSliceVisualElement();
    root.AddToClassList("bg-sub-box--green");
    root.AddToClassList("entity-sub-panel");
    root.Clear();
    return root;
  }

  /// <summary>Wraps the element to make it centered in the UI fragment.</summary>
  public VisualElement CenterElement(VisualElement element) {
    var center = new VisualElement {
        style = {
            justifyContent = Justify.Center,
            flexDirection = FlexDirection.Row,
        },
    };
    center.Add(element);
    return center;
  }

  /// <summary>Creates a label in the game's UI theme.</summary>
  /// <param name="locKey">Optional loc key for the caption.</param>
  /// <param name="classes">
  /// Classes to add to the control. If not set, then the component will be set up for the entity panel.
  /// </param>
  public Label CreateLabel(string locKey = null, string[] classes = null) {
    var label = new Label();
    if (locKey != null) {
      label.text = T(locKey);
    }
    if (classes != null) {
      foreach (var cls in classes) {
        label.AddToClassList(cls);
      }
    } else {
      label.AddToClassList(EntityPanelTextClass);
    }
    return label;
  }

  /// <summary>Creates a text field in the game's UI theme.</summary>
  /// <param name="width">Optional width override.</param>
  /// <param name="classes">Optional classes to add to the control.</param>
  public TextField CreateTextField(int? width = null, string[] classes = null) {
    var textField = new TextField();
    textField.AddToClassList("text-field");
    if (width.HasValue) {
      textField.style.width = width.Value;
    }
    if (classes != null) {
      foreach (var cls in classes) {
        textField.AddToClassList(cls);
      }
    }
    return textField;
  }

  /// <summary>Creates a button in a theme suitable for the right side panel.</summary>
  /// <param name="locKey">Loc key for the caption.</param>
  /// <param name="onClickFn">Callback to call when the button is clicked.</param>
  /// <param name="padding">
  /// Optional padding around the button text. If not set, then <see cref="StandardButtonPadding"/> will be used.
  /// </param>
  /// <param name="classes">
  /// Classes to add to the control. If not set, then the component will be set up for the entity panel.
  /// </param>
  public Button CreateButton(string locKey, Action<Button> onClickFn,
                             (int top, int left, int bottom, int right)? padding = null,
                             string[] classes = null) {
    var button = new NineSliceButton {
        text = T(locKey),
    };
    button.AddToClassList("button-game");
    if (classes != null) {
      foreach (var cls in classes) {
        button.AddToClassList(cls);
      }
    } else {
      button.AddToClassList(EntityPanelTextClass);
    }
    var setPadding = padding ?? StandardButtonPadding;
    button.style.paddingTop = setPadding.top;
    button.style.paddingLeft = setPadding.left;
    button.style.paddingBottom = setPadding.bottom;
    button.style.paddingRight = setPadding.right;
    button.clicked += () => onClickFn(button);
    return button;
  }

  /// <summary>Creates a toggle in the game's UI theme.</summary>
  /// <param name="locKey">Loc key for the caption. If null or empty, then there will be no caption.</param>
  /// <param name="onValueChangedFn">
  /// A callback method that will be called on the value change. The only argument is the new value.
  /// </param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  /// <param name="classes">
  /// Classes to add to the control. If not set, then the component will be set up for the entity panel.
  /// </param>
  public Toggle CreateToggle(string locKey, Action<ChangeEvent<bool>> onValueChangedFn,
                             int spacing = 5, string[] classes = null) {
    var toggle = new Toggle();
    toggle.AddToClassList("game-toggle");
    if (locKey != null) {
      toggle.text = T(locKey);
    }
    if (classes != null) {
      foreach (var cls in classes) {
        toggle.AddToClassList(cls);
      }
    } else {
      toggle.AddToClassList(EntityPanelTextClass);
      toggle.AddToClassList("entity-panel__toggle");
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
  /// <param name="classes">Optional classes to add to the control.</param>
  public Slider CreateSlider(Action<ChangeEvent<float>> onValueChangedFn, float lowValue, float highValue,
                             float stepSize = 0, int spacing = 5, string[] classes = null) {
    var slider = new Slider(lowValue, highValue);
    slider.AddToClassList("slider");
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    if (classes != null) {
      foreach (var cls in classes) {
        slider.AddToClassList(cls);
      }
    }
    slider.RegisterValueChangedCallback(
        evt => {
          if (stepSize > 0) {
            var newValue = Mathf.Round(slider.value / stepSize) * stepSize;
            slider.SetValueWithoutNotify(newValue);
            evt = ChangeEvent<float>.GetPooled(evt.previousValue, newValue);
          }
          onValueChangedFn(evt);
        });
    return slider;
  }

  /// <summary>Creates a min/max slider in the game's UI theme.</summary>
  /// <remarks>
  /// This method requires TimberDev stylesheet. It should either be attached to the element that will become a parent
  /// of the slider, or the stylesheet can be added specifically to the slider via <see cref="AddTimberDevStylesheet"/>.
  /// </remarks>
  /// <param name="lowValue">The minimum value limit.</param>
  /// <param name="highValue">The maximum value limit.</param>
  /// <param name="spacing">If a non-negative value, then the bottom margin will be set.</param>
  /// <seealso cref="AddTimberDevStylesheet"/>
  public MinMaxSlider CreateMinMaxSlider(float lowValue, float highValue, int spacing = 5) {
    var slider = new MinMaxSlider(lowValue, highValue, lowValue, highValue);
    slider.AddToClassList("timberdev-minmax-slider");
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    return slider;
  }

  /// <summary>Creates a scroll view in a theme suitable for the right side panel.</summary>
  /// <remarks>
  /// This method requires TimberDev stylesheet. It should either be attached to the element that will become a parent
  /// of the slider, or the stylesheet can be added specifically to the slider via <see cref="AddTimberDevStylesheet"/>.
  /// </remarks>
  public ScrollView CreateScrollView() {
    var scrollView = new ScrollView();
    scrollView.AddToClassList("timberdev-scroll-view");
    return scrollView;
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
    var slider = new PreciseSlider();
    if (spacing >= 0) {
      slider.style.marginBottom = spacing;
    }
    return new PreciseSliderWrapper(slider, onValueChangedFn, stepSize);
  }

  /// <summary>Adds a handler to the slider that will round the value to the step size.</summary>
  public void AddFixedStepChangeHandler(Slider slider, float stepSize, Action<float> onValueChangedFn) {
    slider.RegisterValueChangedCallback(evt => {
      var adjustedValue = Mathf.Round(evt.newValue / stepSize) * stepSize;
      slider.SetValueWithoutNotify(adjustedValue);
      onValueChangedFn(adjustedValue);
    });
  }

  /// <summary>Adds a handler to the slider that will round the value to the step size.</summary>
  public void AddFixedStepChangeHandler(PreciseSlider slider, float stepSize, Action<float> onValueChangedFn) {
    slider.Initialize(newValue => {
      var adjustedValue = Mathf.Round(newValue / stepSize) * stepSize;
      slider.SetValueWithoutNotify(adjustedValue);
      onValueChangedFn(adjustedValue);
    }, stepSize);
  }

  /// <summary>Adds a handler to the slider that will round the value to the step size.</summary>
  public void AddFixedStepChangeHandler(MinMaxSlider slider, float stepSize, float minDelta,
                                        Action<Vector2> onValueChangedFn) {
    slider.RegisterValueChangedCallback(
        e => {
          var newValue = e.newValue;
          if (stepSize > 0) {
            newValue = new Vector2(
                Mathf.Round(e.newValue.x / stepSize) * stepSize, Mathf.Round(e.newValue.y / stepSize) * stepSize);
          }
          if (newValue.y - newValue.x < minDelta) {
            if (Math.Abs(e.previousValue.x - newValue.x) < float.Epsilon) {
              newValue.y = newValue.x + minDelta;
            } else {
              newValue.x = newValue.y - minDelta;
            }
          }
          slider.SetValueWithoutNotify(newValue);
          onValueChangedFn(newValue);
        });
  }

  /// <summary>Creates a simple dropdown in a theme suitable for the right side panel.</summary>
  /// <remarks>
  /// This dropdown will resize itself to the largest item, assigned via
  /// <see cref="ResizableDropdownElement.SetItems"/>. If the maximum width is needed, then set it manually.
  /// </remarks>
  public ResizableDropdownElement CreateSimpleDropdown(Action<string> onValueChanged = null) {
    var dropdown = new ResizableDropdownElement();
    dropdown.Initialize(_dropdownListDrawer, _visualElementLoader);
    if (onValueChanged != null) {
      dropdown.OnValueChanged += (_, _) => onValueChanged(dropdown.SelectedValue);
    }
    return dropdown;
  }

  /// <summary>Adds the TimberDev stylesheet to the elements that needs it.</summary>
  /// <remarks>
  /// The stylesheet can be added to a specific element that needs it, or it can be added once to the root element.
  /// </remarks>
  /// <seealso cref="CreateMinMaxSlider"/>
  public void AddTimberDevStylesheet(VisualElement element) {
    element.styleSheets.Add(TimberDevStylesheet);
  }

  /// <summary>Loads a visual tree asset from the "UI/Views" folder.</summary>
  /// <remarks>
  /// The template gets properly transformed into VisualElement, and the game controls get initialized. All the
  /// stylesheets on the template will be preserved. Use this method when a dialog or a panel is being loaded.
  /// </remarks>
  public VisualElement LoadVisualTreeAsset(string name) {
    var element = _assetLoader.Load<VisualTreeAsset>(UIViewsPath + name).Instantiate();
    _visualElementInitializer.InitializeVisualElement(element);
    return element;
  }

  /// <summary>Loads a visual element from the "UI/Views" folder.</summary>
  /// <remarks>
  /// If the name is a template, then the first visual element gets extracted adn returned, and all the others (if any)
  /// will be ignored. The game controls will be properly initialized. The stylesheets aren't preserved! The element,
  /// once added to the hierarchy, will inherit the styles from its parent.
  /// </remarks>
  /// <seealso cref="LoadVisualTreeAsset"/>
  public T LoadVisualElement<T>(string name) where T : VisualElement {
    return _visualElementLoader.LoadVisualElement(name).Q<T>();
  }

  /// <inheritdoc cref="LoadVisualElement{T}"/>
  public VisualElement LoadVisualElement(string name) {
    return _visualElementLoader.LoadVisualElement(name);
  }

  #region Implementation

  readonly VisualElementLoader _visualElementLoader;
  readonly ILoc _loc;
  readonly IAssetLoader _assetLoader;
  readonly VisualElementInitializer _visualElementInitializer;
  readonly DropdownListDrawer _dropdownListDrawer;

  UiFactory(VisualElementLoader visualElementLoader, ILoc loc, IAssetLoader assetLoader,
            VisualElementInitializer visualElementInitializer, DropdownListDrawer dropdownListDrawer) {
    _visualElementLoader = visualElementLoader;
    _loc = loc;
    _assetLoader = assetLoader;
    _visualElementInitializer = visualElementInitializer;
    _dropdownListDrawer = dropdownListDrawer;
  }

  #endregion
}

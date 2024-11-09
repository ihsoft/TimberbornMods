// Decompiled with JetBrains decompiler
// Type: UnityEngine.UIElements.MinMaxSlider
// Assembly: UnityEngine.UIElementsModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null

// This is a fixed version of the original MinMaxSlider. The original one was causing "Layout update is struggling to
// process current layout" error on certain slider positions.
// See: https://discussions.unity.com/t/unity-6-uitoolkit-layout-update-is-struggling-to-process-current-layout-with-minmaxslider-in-editor/1538464

using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Internal;
using UnityEngine.UIElements;

namespace IgorZ.TimberDev.UI;

public class MinMaxSlider2 : BaseField<Vector2> {
  internal static readonly BindingId minValueProperty = (BindingId)nameof(minValue);
  internal static readonly BindingId maxValueProperty = (BindingId)nameof(maxValue);
  internal static readonly BindingId rangeProperty = (BindingId)nameof(range);
  internal static readonly BindingId lowLimitProperty = (BindingId)nameof(lowLimit);
  internal static readonly BindingId highLimitProperty = (BindingId)nameof(highLimit);
  Vector2 m_DragElementStartPos;
  Vector2 m_ValueStartPos;
  DragState m_DragState;
  float m_MinLimit;
  float m_MaxLimit;
  internal const float kDefaultHighValue = 10f;
  public new static readonly string ussClassName = "unity-min-max-slider";
  public new static readonly string labelUssClassName = ussClassName + "__label";
  public new static readonly string inputUssClassName = ussClassName + "__input";
  public static readonly string trackerUssClassName = ussClassName + "__tracker";
  public static readonly string draggerUssClassName = ussClassName + "__dragger";
  public static readonly string minThumbUssClassName = ussClassName + "__min-thumb";
  public static readonly string maxThumbUssClassName = ussClassName + "__max-thumb";
  public static readonly string movableUssClassName = ussClassName + "--movable";

  internal VisualElement dragElement { get; }

  internal VisualElement dragMinThumb { get; }

  internal VisualElement dragMaxThumb { get; }

  internal ClampedDragger<float> clampedDragger { get; }

  [CreateProperty]
  public float minValue {
    get => value.x;
    set {
      var minValue = this.minValue;
      base.value = ClampValues(new Vector2(value, rawValue.y));
      if (Mathf.Approximately(minValue, this.minValue)) {
        return;
      }
      NotifyPropertyChanged(in minValueProperty);
    }
  }

  [CreateProperty]
  public float maxValue {
    get => value.y;
    set {
      var maxValue = this.maxValue;
      base.value = ClampValues(new Vector2(rawValue.x, value));
      if (Mathf.Approximately(maxValue, this.maxValue)) {
        return;
      }
      NotifyPropertyChanged(in maxValueProperty);
    }
  }

  public override Vector2 value {
    get => base.value;
    set => base.value = ClampValues(value);
  }

  public override void SetValueWithoutNotify(Vector2 newValue) {
    base.SetValueWithoutNotify(ClampValues(newValue));
    UpdateDragElementPosition();
  }

  [CreateProperty(ReadOnly = true)] public float range => Math.Abs(highLimit - lowLimit);

  [CreateProperty]
  public float lowLimit {
    get => m_MinLimit;
    set {
      if (Mathf.Approximately(m_MinLimit, value)) {
        return;
      }
      m_MinLimit = value <= (double)m_MaxLimit
          ? value
          : throw new ArgumentException("lowLimit is greater than highLimit");
      this.value = rawValue;
      UpdateDragElementPosition();
      if (!string.IsNullOrEmpty(viewDataKey)) {
        this.SaveViewData();
      }
      NotifyPropertyChanged(in lowLimitProperty);
    }
  }

  [CreateProperty]
  public float highLimit {
    get => m_MaxLimit;
    set {
      if (Mathf.Approximately(m_MaxLimit, value)) {
        return;
      }
      m_MaxLimit = value >= (double)m_MinLimit
          ? value
          : throw new ArgumentException("highLimit is smaller than lowLimit");
      this.value = rawValue;
      UpdateDragElementPosition();
      if (!string.IsNullOrEmpty(viewDataKey)) {
        this.SaveViewData();
      }
      NotifyPropertyChanged(in highLimitProperty);
    }
  }

  public MinMaxSlider2() : this(null) {
  }

  public MinMaxSlider2(float minValue, float maxValue, float minLimit, float maxLimit) : this(
      null, minValue, maxValue, minLimit, maxLimit) {
  }

  public MinMaxSlider2(string label, float minValue = 0.0f, float maxValue = 10f, float minLimit = -3.4028235E+38f,
                      float maxLimit = 3.4028235E+38f) : base(label, null) {
    m_MinLimit = float.MinValue;
    m_MaxLimit = float.MaxValue;
    lowLimit = minLimit;
    highLimit = maxLimit;
    var vector2 = ClampValues(new Vector2(minValue, maxValue));
    this.minValue = vector2.x;
    this.maxValue = vector2.y;
    AddToClassList(ussClassName);
    labelElement.AddToClassList(labelUssClassName);
    this.visualInput.AddToClassList(inputUssClassName);
    pickingMode = PickingMode.Ignore;
    m_DragState = DragState.NoThumb;
    this.visualInput.pickingMode = PickingMode.Position;
    var child = new VisualElement {
        name = "unity-tracker"
    };
    child.AddToClassList(trackerUssClassName);
    this.visualInput.Add(child);
    dragElement = new VisualElement {
        name = "unity-dragger"
    };
    dragElement.AddToClassList(draggerUssClassName);
    dragElement.RegisterCallback(new EventCallback<GeometryChangedEvent>(UpdateDragElementPosition));
    this.visualInput.Add(dragElement);
    dragMinThumb = new VisualElement {
        name = "unity-thumb-min"
    };
    dragMaxThumb = new VisualElement {
        name = "unity-thumb-max"
    };
    dragMinThumb.AddToClassList(minThumbUssClassName);
    dragMaxThumb.AddToClassList(maxThumbUssClassName);
    dragElement.Add(dragMinThumb);
    dragElement.Add(dragMaxThumb);
    clampedDragger = new ClampedDragger<float>(null, SetSliderValueFromClick, SetSliderValueFromDrag);
    this.visualInput.AddManipulator(clampedDragger);
    m_MinLimit = minLimit;
    m_MaxLimit = maxLimit;
    rawValue = ClampValues(new Vector2(minValue, maxValue));
    UpdateDragElementPosition();
    RegisterCallback(new EventCallback<FocusInEvent>(OnFocusIn));
    RegisterCallback(new EventCallback<BlurEvent>(OnBlur));
    RegisterCallback(new EventCallback<NavigationSubmitEvent>(OnNavigationSubmit));
    RegisterCallback(new EventCallback<NavigationMoveEvent>(OnNavigationMove));
  }

  Vector2 ClampValues(Vector2 valueToClamp) {
    if (m_MinLimit > (double)m_MaxLimit) {
      m_MinLimit = m_MaxLimit;
    }
    var vector2 = new Vector2();
    if (valueToClamp.y > (double)m_MaxLimit) {
      valueToClamp.y = m_MaxLimit;
    }
    vector2.x = Mathf.Clamp(valueToClamp.x, m_MinLimit, valueToClamp.y);
    vector2.y = Mathf.Clamp(valueToClamp.y, valueToClamp.x, m_MaxLimit);
    return vector2;
  }

  void UpdateDragElementPosition(GeometryChangedEvent evt) {
    var rect = evt.oldRect;
    var size1 = rect.size;
    rect = evt.newRect;
    var size2 = rect.size;
    if (size1 == size2) {
      return;
    }
    UpdateDragElementPosition();
  }

  // THis method was fixed to use rounded widths. 
  void UpdateDragElementPosition() {
    if (panel == null) {
      return;
    }
    var num1 = dragElement.resolvedStyle.borderLeftWidth + dragElement.resolvedStyle.marginLeft;
    var num2 = dragElement.resolvedStyle.borderRightWidth + dragElement.resolvedStyle.marginRight;
    var num3 = num2 + num1;
    var dragMinThumbWidth = Mathf.Round(dragMinThumb.resolvedStyle.width);
    var dragMaxThumbWidth = Mathf.Round(dragMaxThumb.resolvedStyle.width);
    var num5 = Mathf.Round(
        SliderLerpUnclamped(
            dragMinThumbWidth, this.visualInput.layout.width - dragMaxThumbWidth - num3,
            SliderNormalizeValue(minValue, lowLimit, highLimit)));
    dragElement.style.width = Mathf.Round(
            SliderLerpUnclamped(
                dragMinThumbWidth + num3,
                this.visualInput.layout.width - dragMaxThumbWidth,
                SliderNormalizeValue(maxValue, lowLimit, highLimit)))
        - num5;
    dragElement.style.left = num5;
    dragMinThumb.style.left = -dragMinThumbWidth - num1;
    dragMaxThumb.style.right = -dragMaxThumbWidth - num2;
  }

  internal float SliderLerpUnclamped(float a, float b, float interpolant) {
    return Mathf.LerpUnclamped(a, b, interpolant);
  }

  internal float SliderNormalizeValue(float currentValue, float lowerValue, float higherValue) {
    return (float)((currentValue - (double)lowerValue) / (higherValue - (double)lowerValue));
  }

  float ComputeValueFromPosition(float positionToConvert) {
    return SliderLerpUnclamped(
        lowLimit, highLimit, SliderNormalizeValue(positionToConvert, 0.0f, this.visualInput.layout.width));
  }

  [EventInterest(typeof(GeometryChangedEvent))]
  //protected override void HandleEventBubbleUp(EventBase evt) {
  public override void HandleEventBubbleUp(EventBase evt) {
    base.HandleEventBubbleUp(evt);
    if (evt == null || evt.eventTypeId != EventBase<GeometryChangedEvent>.TypeId()) {
      return;
    }
    UpdateDragElementPosition((GeometryChangedEvent)evt);
  }

  DragState GetNavigationState() {
    var flag1 = dragMinThumb.ClassListContains(movableUssClassName);
    var flag2 = dragMaxThumb.ClassListContains(movableUssClassName);
    if (flag1) {
      return flag2 ? DragState.MiddleThumb : DragState.MinThumb;
    }
    return flag2 ? DragState.MaxThumb : DragState.NoThumb;
  }

  void SetNavigationState(DragState newState) {
    dragMinThumb.EnableInClassList(
        movableUssClassName, newState == DragState.MinThumb || newState == DragState.MiddleThumb);
    dragMaxThumb.EnableInClassList(
        movableUssClassName, newState == DragState.MaxThumb || newState == DragState.MiddleThumb);
    dragElement.EnableInClassList(movableUssClassName, newState == DragState.MiddleThumb);
  }

  void OnFocusIn(FocusInEvent evt) {
    if (GetNavigationState() != DragState.NoThumb) {
      return;
    }
    SetNavigationState(DragState.MinThumb);
  }

  void OnBlur(BlurEvent evt) {
    SetNavigationState(DragState.NoThumb);
  }

  void OnNavigationSubmit(NavigationSubmitEvent evt) {
    var newState = GetNavigationState() + 1;
    if (newState > DragState.NoThumb) {
      newState = DragState.MinThumb;
    }
    SetNavigationState(newState);
  }

  void OnNavigationMove(NavigationMoveEvent evt) {
    var navigationState = GetNavigationState();
    if (navigationState == DragState.NoThumb
        || evt.direction != NavigationMoveEvent.Direction.Left
        && evt.direction != NavigationMoveEvent.Direction.Right) {
      return;
    }
    ComputeValueFromKey(evt.direction == NavigationMoveEvent.Direction.Left, evt.shiftKey, navigationState);
    evt.StopPropagation();
    focusController?.IgnoreEvent(evt);
  }

  void ComputeValueFromKey(bool leftDirection, bool isShift, DragState moveState) {
    var f = BaseSlider<float>.GetClosestPowerOfTen(
        Mathf.Abs((float)((highLimit - (double)lowLimit) * 0.009999999776482582)));
    if (isShift) {
      f *= 10f;
    }
    if (leftDirection) {
      f = -f;
    }
    switch (moveState) {
      case DragState.MinThumb:
        value = new Vector2(
            Mathf.Clamp(BaseSlider<float>.RoundToMultipleOf(value.x + f * 0.5001f, Mathf.Abs(f)), lowLimit, value.y),
            value.y);
        break;
      case DragState.MaxThumb:
        value = new Vector2(
            value.x,
            Mathf.Clamp(BaseSlider<float>.RoundToMultipleOf(value.y + f * 0.5001f, Mathf.Abs(f)), value.x, highLimit));
        break;
      case DragState.MiddleThumb:
        var num = value.y - value.x;
        if (f > 0.0) {
          float y = Mathf.Clamp(
              BaseSlider<float>.RoundToMultipleOf(value.y + f * 0.5001f, Mathf.Abs(f)), value.x, highLimit);
          value = new Vector2(y - num, y);
          break;
        }
        float x = Mathf.Clamp(
            BaseSlider<float>.RoundToMultipleOf(value.x + f * 0.5001f, Mathf.Abs(f)), lowLimit, value.y);
        value = new Vector2(x, x + num);
        break;
    }
  }

  void SetSliderValueFromDrag() {
    if (clampedDragger.dragDirection != ClampedDragger<float>.DragDirection.Free) {
      return;
    }
    var x = m_DragElementStartPos.x;
    var dragElementEndPos = x + clampedDragger.delta.x;
    ComputeValueFromDraggingThumb(x, dragElementEndPos);
  }

  void SetSliderValueFromClick() {
    if (clampedDragger.dragDirection == ClampedDragger<float>.DragDirection.Free) {
      return;
    }
    var world = this.visualInput.LocalToWorld(clampedDragger.startMousePosition);
    Rect layout;
    if (dragMinThumb.worldBound.Contains(world)) {
      m_DragState = DragState.MinThumb;
    } else if (dragMaxThumb.worldBound.Contains(world)) {
      m_DragState = DragState.MaxThumb;
    } else {
      double x1 = clampedDragger.startMousePosition.x;
      layout = dragElement.layout;
      double xMin = layout.xMin;
      int num;
      if (x1 > xMin) {
        double x2 = clampedDragger.startMousePosition.x;
        layout = dragElement.layout;
        double xMax = layout.xMax;
        num = x2 < xMax ? 1 : 0;
      } else {
        num = 0;
      }
      m_DragState = num == 0 ? DragState.NoThumb : DragState.MiddleThumb;
    }
    if (m_DragState == DragState.NoThumb) {
      var valueFromPosition = ComputeValueFromPosition(clampedDragger.startMousePosition.x);
      double x3 = clampedDragger.startMousePosition.x;
      layout = dragElement.layout;
      double x4 = layout.x;
      if (x3 < x4) {
        m_DragState = DragState.MinThumb;
        value = new Vector2(valueFromPosition, value.y);
      } else {
        m_DragState = DragState.MaxThumb;
        value = new Vector2(value.x, valueFromPosition);
      }
    }
    SetNavigationState(m_DragState);
    m_ValueStartPos = value;
    clampedDragger.dragDirection = ClampedDragger<float>.DragDirection.Free;
    m_DragElementStartPos = clampedDragger.startMousePosition;
  }

  void ComputeValueFromDraggingThumb(float dragElementStartPos, float dragElementEndPos) {
    var valueFromPosition = ComputeValueFromPosition(dragElementStartPos);
    var num1 = ComputeValueFromPosition(dragElementEndPos) - valueFromPosition;
    SetNavigationState(m_DragState);
    switch (m_DragState) {
      case DragState.MinThumb:
        var x = m_ValueStartPos.x + num1;
        if (x > (double)maxValue) {
          x = maxValue;
        } else if (x < (double)lowLimit) {
          x = lowLimit;
        }
        value = new Vector2(x, maxValue);
        break;
      case DragState.MaxThumb:
        var y = m_ValueStartPos.y + num1;
        if (y < (double)minValue) {
          y = minValue;
        } else if (y > (double)highLimit) {
          y = highLimit;
        }
        value = new Vector2(minValue, y);
        break;
      case DragState.MiddleThumb:
        var vector2 = value with {
            x = m_ValueStartPos.x + num1,
            y = m_ValueStartPos.y + num1
        };
        var num2 = m_ValueStartPos.y - m_ValueStartPos.x;
        if (vector2.x < (double)lowLimit) {
          vector2.x = lowLimit;
          vector2.y = lowLimit + num2;
        } else if (vector2.y > (double)highLimit) {
          vector2.y = highLimit;
          vector2.x = highLimit - num2;
        }
        value = vector2;
        break;
    }
  }

  //protected override void UpdateMixedValueContent() {
  public override void UpdateMixedValueContent() {
  }

  [ExcludeFromDocs]
  [Serializable]
  public new class
      UxmlSerializedData : BaseField<Vector2>.UxmlSerializedData, IUxmlSerializedDataCustomAttributeHandler {
    [SerializeField] float lowLimit;

    [HideInInspector] [UxmlIgnore] [SerializeField]
    UxmlAttributeFlags lowLimit_UxmlAttributeFlags;

    [SerializeField] float highLimit;

    [UxmlIgnore] [SerializeField] [HideInInspector]
    UxmlAttributeFlags highLimit_UxmlAttributeFlags;

    void IUxmlSerializedDataCustomAttributeHandler.SerializeCustomAttributes(
        IUxmlAttributes bag, HashSet<string> handledAttributes) {
      var foundAttributeCounter = 0;
      var floatAttribute1 = UxmlUtility.TryParseFloatAttribute("min-value", bag, ref foundAttributeCounter);
      var floatAttribute2 = UxmlUtility.TryParseFloatAttribute("max-value", bag, ref foundAttributeCounter);
      if (foundAttributeCounter <= 0) {
        return;
      }
      this.Value = new Vector2(floatAttribute1, floatAttribute2);
      handledAttributes.Add("value");
      if (bag is UxmlAsset uxmlAsset) {
        uxmlAsset.RemoveAttribute("min-value");
        uxmlAsset.RemoveAttribute("max-value");
        uxmlAsset.SetAttribute("value", UxmlUtility.ValueToString(this.Value));
      }
    }

    public override object CreateInstance() {
      return new MinMaxSlider2();
    }

    public override void Deserialize(object obj) {
      base.Deserialize(obj);
      var minMaxSlider = (MinMaxSlider2)obj;
      if (ShouldWriteAttributeValue(lowLimit_UxmlAttributeFlags)) {
        minMaxSlider.lowLimit = lowLimit;
      }
      if (!ShouldWriteAttributeValue(highLimit_UxmlAttributeFlags)) {
        return;
      }
      minMaxSlider.highLimit = highLimit;
    }
  }

  enum DragState {
    MinThumb,
    MaxThumb,
    MiddleThumb,
    NoThumb
  }
}

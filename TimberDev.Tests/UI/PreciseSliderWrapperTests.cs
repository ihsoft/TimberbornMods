using IgorZ.TimberDev.UI;
using Timberborn.CoreUI;

namespace TimberDev.Tests;

static class PreciseSliderWrapperTests {
  public static void RoundsChangedValueToStepAndInvokesCallback() {
    var slider = new PreciseSlider();
    var callbackValue = 0f;
    var wrapper = new PreciseSliderWrapper(slider, value => callbackValue = value, 0.5f);

    slider.TriggerValueChanged(1.26f);

    Assert.Equal(0.5f, slider.Step);
    Assert.Equal(1.5f, slider.Value);
    Assert.Equal(1.5f, wrapper.Value);
    Assert.Equal(1.5f, callbackValue);
  }

  public static void UpdatesValuesWithoutCallback() {
    var slider = new PreciseSlider();
    var callbackCount = 0;
    var wrapper = new PreciseSliderWrapper(slider, value => callbackCount++, 0.25f);

    wrapper.UpdateValuesWithoutNotify(2.25f, 5f);
    wrapper.SetValueWithoutNotify(3f);

    Assert.Equal(0, callbackCount);
    Assert.Equal(3f, wrapper.Value);
    Assert.Equal(5f, slider.MaxValue);
  }
}

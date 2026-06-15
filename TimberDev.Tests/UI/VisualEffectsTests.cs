using IgorZ.TimberDev.UI;
using UnityEngine.UIElements;

namespace TimberDev.Tests;

static class VisualEffectsTests {
  public static void SchedulesSwitchEffect() {
    var element = new VisualElement();
    var lastState = "";

    VisualEffects.ScheduleSwitchEffect(element, 100, "start", "end", (e, state) => lastState = state);
    Assert.Equal("start", lastState);

    ((TestScheduler)element.schedule).Actions[0]();
    Assert.Equal("end", lastState);
  }

  public static void TemporarilySetsClass() {
    var element = new VisualElement();

    VisualEffects.SetTemporaryClass(element, 100, "flash");
    Assert.True(element.ClassListContains("flash"));

    ((TestScheduler)element.schedule).Actions[0]();
    Assert.False(element.ClassListContains("flash"));
  }
}

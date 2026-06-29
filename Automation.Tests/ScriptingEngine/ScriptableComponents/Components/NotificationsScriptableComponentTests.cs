using System;
using System.Collections.Generic;
using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using Timberborn.BlockSystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.WorldPersistence;

namespace Automation.Tests;

static class NotificationsScriptableComponentTests {
  public static void ExposesNotificationActions() {
    var component = CreateComponent();

    var actionNames = component.GetActionNamesForBuilding(new AutomationBehavior());

    Assert.Equal("Notifications.SetNoticeIcon", actionNames[0]);
    Assert.Equal("Notifications.SetAlert", actionNames[1]);
    Assert.Equal("Notifications.ClearStatus", actionNames[2]);
  }

  public static void BuildsActionDefinitions() {
    var component = CreateComponent();

    var noticeDef = component.GetActionDefinition("Notifications.SetNoticeIcon", new AutomationBehavior());
    var alertDef = component.GetActionDefinition("Notifications.SetAlert", new AutomationBehavior());
    var clearDef = component.GetActionDefinition("Notifications.ClearStatus", new AutomationBehavior());

    Assert.Equal("Notifications.SetNoticeIcon", noticeDef.ScriptName);
    Assert.Equal(2, noticeDef.Arguments.Length);
    Assert.True(noticeDef.Arguments[0].Options.Length > 0);
    Assert.Equal("Notifications.SetAlert", alertDef.ScriptName);
    Assert.Equal(3, alertDef.Arguments.Length);
    Assert.Equal("Notifications.ClearStatus", clearDef.ScriptName);
    Assert.Equal(0, clearDef.Arguments.Length);
  }

  public static void ExecutesNoticeAlertAndClearActions() {
    var component = CreateComponent();
    var behavior = CreateBehavior();
    var statusSubject = behavior.GetComponent<StatusSubject>();

    component.GetActionExecutor("Notifications.SetNoticeIcon", behavior)(
        [ScriptValue.FromString("ApiStopped"), ScriptValue.FromString("notice")]);

    Assert.Equal(1, statusSubject.RegisteredStatuses.Count);
    Assert.True(statusSubject.RegisteredStatuses[0].IsActive);

    component.GetActionExecutor("Notifications.SetAlert", behavior)(
        [ScriptValue.FromString("Hunger"), ScriptValue.FromString("status"), ScriptValue.FromString("alert")]);

    Assert.Equal(2, statusSubject.RegisteredStatuses.Count);
    Assert.False(statusSubject.RegisteredStatuses[0].IsActive);
    Assert.True(statusSubject.RegisteredStatuses[1].IsActive);

    component.GetActionExecutor("Notifications.ClearStatus", behavior)([]);

    Assert.False(statusSubject.RegisteredStatuses[1].IsActive);
  }

  public static void InstallsAndUninstallsActions() {
    var component = CreateComponent();
    var behavior = CreateBehavior();
    var action = CreateAction("Notifications.SetAlert");

    component.InstallAction(action, behavior);

    Assert.True(behavior.GetOrThrow<NotificationsScriptableComponent.StatusController>().HasActions);

    component.UninstallAction(action, behavior);

    Assert.False(behavior.GetOrThrow<NotificationsScriptableComponent.StatusController>().HasActions);
  }

  public static void ReportsUnknownAction() {
    var component = CreateComponent();
    var behavior = CreateBehavior();
    var action = CreateAction("Notifications.SetAlert");
    var unknownAction = CreateAction("Notifications.Missing");

    Assert.Throws<ScriptError.ParsingError>(() => component.GetActionDefinition("Notifications.Missing", behavior));
    Assert.Throws<InvalidOperationException>(() => component.InstallAction(unknownAction, behavior));
    component.InstallAction(action, behavior);
    Assert.Throws<InvalidOperationException>(() => component.UninstallAction(unknownAction, behavior));
  }

  public static void StatusControllerLoadIgnoresMissingStatusState() {
    var loader = new TestEntityLoader();
    loader.SetComponent(
        new ComponentKey(typeof(NotificationsScriptableComponent.StatusController).FullName),
        new IObjectLoader(new Dictionary<string, object>()));

    new NotificationsScriptableComponent.StatusController().Load(loader);
  }

  static NotificationsScriptableComponent CreateComponent() {
    var component = new NotificationsScriptableComponent();
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.InjectDependencies(new StatusSpriteLoader());
    return component;
  }

  static AutomationBehavior CreateBehavior() {
    SetDependencyContainer(new TestContainer());
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new BlockObject());
    behavior.SetComponent(new StatusSubject());
    behavior.InjectDependencies(new AutomationService());
    behavior.Awake();
    return behavior;
  }

  static ActionOperator CreateAction(string name) {
    var notifications = new TestScriptable("Notifications");
    notifications.RegisterAction(name);
    TestScripting.CreateService(notifications);
    return ActionOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, name, []);
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }

}

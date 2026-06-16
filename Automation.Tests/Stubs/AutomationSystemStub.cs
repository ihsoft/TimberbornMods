using System;
using System.Collections.Generic;
using Timberborn.Localization;

namespace IgorZ.Automation.AutomationSystem;

public sealed class AutomationService {
  public ILoc Loc { get; } = new FakeLoc();
  public AutomationService Instance => this;
  public readonly List<AutomationBehavior> RegisteredBehaviors = [];
  public readonly List<AutomationBehavior> UnregisteredBehaviors = [];

  public void RegisterBehavior(AutomationBehavior behavior) {
    RegisteredBehaviors.Add(behavior);
  }

  public void UnregisterBehavior(AutomationBehavior behavior) {
    UnregisteredBehaviors.Add(behavior);
  }

  public void ScheduleLateUpdate(Action action) {
    action();
  }
}

public interface IGameSerializable {
  bool IsMarkedForCleanup { get; }
}

public interface IAutomationCondition : IGameSerializable {
  AutomationBehavior Behavior { get; set; }
  bool IsEnabled { get; }
  bool CanRunOnUnfinishedBuildings { get; }

  void Activate();
  IAutomationCondition CloneDefinition();
}

public interface IAutomationAction : IGameSerializable {
  AutomationBehavior Behavior { get; set; }
  IAutomationCondition Condition { get; set; }
  string TemplateFamily { get; }

  IAutomationAction CloneDefinition();
}

sealed class FakeLoc : ILoc {
  public string T(string key, params object[] args) {
    return key;
  }
}

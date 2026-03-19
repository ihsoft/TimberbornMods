// Timberborn Mod: Automation
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using System;
using System.Collections.Generic;
using System.Linq;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.TimberDev.UI;
using IgorZ.TimberDev.Utils;
using ProtoBuf;
using Timberborn.Persistence;
using Timberborn.StatusSystem;
using Timberborn.WorldPersistence;

namespace IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;

sealed class NotificationsScriptableComponent : ScriptableComponentBase {

  const string ClearStatusActionLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.ClearStatus";
  const string SetAlertActionLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.SetAlert";
  const string SetStatusActionLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.SetStatus";
  const string SetStatusActionAlertTextLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.SetStatusAlertTextArgument";
  const string SetStatusActionIconLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.SetStatusIconArgument";
  const string SetStatusActionTextLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.SetStatusTextArgument";
  const string SetNoticeIconActionLocKey = "IgorZ.Automation.Scriptable.Notifications.Action.SetNoticeIcon";

  const string ClearStatusActionName = "Notifications.ClearStatus";
  const string SetAlertActionName = "Notifications.SetAlert";
  const string SetNoticeIconActionName = "Notifications.SetNoticeIcon";
  const string SetStatusActionName = "Notifications.SetStatus";

  static readonly List<string> AlertStatusIcons = [
      "IgorZ.Automation/Notification", "IgorZ.Automation/Alarm",
      "ApiStopped",
      "Hunger", "Thirst", "Exhaustion", "NothingToDo", "OutOfFuel", "BadwaterContamination",
      "GenericError",
      "NotEnoughWater", "NoStorage", "LackOfResources",
      "Death",
      "WellbeingHighscore",
  ];

  static readonly List<string> NoticeStatusIcons = AlertStatusIcons;

  #region ScriptableComponentBase implementation

  /// <inheritdoc/>
  public override string Name => "Notifications";

  /// <inheritdoc/>
  public override string[] GetActionNamesForBuilding(AutomationBehavior behavior) {
    return [SetNoticeIconActionName, SetAlertActionName, ClearStatusActionName];
  }

  /// <inheritdoc/>
  public override Action<ScriptValue[]> GetActionExecutor(string name, AutomationBehavior behavior) {
    var statusController = behavior.GetOrCreate<StatusController>();
    return name switch {
        SetNoticeIconActionName => args => SetNoticeStatusAction(args, statusController),
        SetAlertActionName => args => SetAlertStatusAction(args, statusController),
        SetStatusActionName => args => SetStatusAction(args, statusController),
        ClearStatusActionName => args => ClearStatusAction(args, statusController),
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override ActionDef GetActionDefinition(string name, AutomationBehavior behavior) {
    return name switch {
        SetNoticeIconActionName => SetNoticeIconActionDef,
        SetAlertActionName => SetAlertActionDef,
        SetStatusActionName => SetStatusActionDef,
        ClearStatusActionName => ClearStatusActionDef,
        _ => throw new UnknownActionException(name),
    };
  }

  /// <inheritdoc/>
  public override void InstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is not SetNoticeIconActionName
        and not SetAlertActionName and not SetStatusActionName and not ClearStatusActionName) {
      throw new InvalidOperationException($"Unknown action: {actionOperator.ActionName}");
    }
    behavior.GetOrCreate<StatusController>().AddAction(actionOperator);
  }

  /// <inheritdoc/>
  public override void UninstallAction(ActionOperator actionOperator, AutomationBehavior behavior) {
    if (actionOperator.ActionName is not SetNoticeIconActionName
        and not SetAlertActionName and not SetStatusActionName and not ClearStatusActionName) {
      throw new InvalidOperationException($"Unknown action: {actionOperator.ActionName}");
    }
    behavior.GetOrThrow<StatusController>().RemoveAction(actionOperator);
  }

  #endregion

  #region Actions

  ActionDef SetStatusActionDef => _setStatusDef ??= new ActionDef {
      ScriptName = SetStatusActionName,
      DisplayName = Loc.T(SetStatusActionLocKey),
      Arguments = [
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionIconLocKey),
              ValueType = ScriptValue.TypeEnum.String,
          },
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionTextLocKey),
              ValueType = ScriptValue.TypeEnum.String,
          },
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionAlertTextLocKey),
              ValueType = ScriptValue.TypeEnum.String,
          },
          new ValueDef {
              DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
              ValueType = ScriptValue.TypeEnum.Number,
          },
          new ValueDef {
              DisplayNumericFormat = ValueDef.NumericFormatEnum.Integer,
              ValueType = ScriptValue.TypeEnum.Number,
          },
      ],
  };
  ActionDef _setStatusDef;

  ActionDef SetNoticeIconActionDef => _setNoticeIconDef ??= new ActionDef {
      ScriptName = SetNoticeIconActionName,
      DisplayName = Loc.T(SetNoticeIconActionLocKey),
      Arguments = [
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionIconLocKey),
              ValueType = ScriptValue.TypeEnum.String,
              Options = NoticeStatusIcons.Select(MakeStatusOption).ToArray(),
          },
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionTextLocKey),
              ValueType = ScriptValue.TypeEnum.String,
          },
      ],
  };
  ActionDef _setNoticeIconDef;

  ActionDef SetAlertActionDef => _setAlertDef ??= new ActionDef {
      ScriptName = SetAlertActionName,
      DisplayName = Loc.T(SetAlertActionLocKey),
      Arguments = [
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionIconLocKey),
              ValueType = ScriptValue.TypeEnum.String,
              Options = AlertStatusIcons.Select(MakeStatusOption).ToArray(),
          },
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionTextLocKey),
              ValueType = ScriptValue.TypeEnum.String,
          },
          new ValueDef {
              DisplayName = Loc.T(SetStatusActionAlertTextLocKey),
              ValueType = ScriptValue.TypeEnum.String,
          },
      ],
  };
  ActionDef _setAlertDef;

  ActionDef ClearStatusActionDef => _clearStatusDef ??= new ActionDef {
      ScriptName = ClearStatusActionName,
      DisplayName = Loc.T(ClearStatusActionLocKey),
      Arguments = [],
  };
  ActionDef _clearStatusDef;

  static void SetStatusAction(ScriptValue[] args, StatusController statusController) {
    AssertActionArgsCount(SetStatusActionName, args, 5);
    statusController.SetStatusState(new StatusController.StatusDef {
        SpriteName = args[0].AsString,
        StatusTextLocKey = args[2].AsString,
        AlertTextLocKey = args[2].AsString,
        NeedFloatingIcon = args[3].AsRawNumber > 0,
        IsPriority = args[4].AsRawNumber > 0,
    }, true);
  }

  static void SetNoticeStatusAction(ScriptValue[] args, StatusController statusController) {
    AssertActionArgsCount(SetStatusActionName, args, 2);
    statusController.SetStatusState(new StatusController.StatusDef {
        SpriteName = args[0].AsString,
        StatusTextLocKey = args[1].AsString,
        NeedFloatingIcon = true,
    }, true);
  }

  static void SetAlertStatusAction(ScriptValue[] args, StatusController statusController) {
    AssertActionArgsCount(SetStatusActionName, args, 3);
    statusController.SetStatusState(new StatusController.StatusDef {
        SpriteName = args[0].AsString,
        StatusTextLocKey = args[1].AsString,
        AlertTextLocKey = args[2].AsString,
    }, true);
  }

  static void ClearStatusAction(ScriptValue[] args, StatusController statusController) {
    AssertActionArgsCount(SetStatusActionName, args, 0);
    statusController.ClearCurrentStatus();
  }

  #endregion

  #region Implementation

  StatusSpriteLoader _statusSpriteLoader;

  [Inject]
  public void InjectDependencies(StatusSpriteLoader statusSpriteLoader) {
    _statusSpriteLoader = statusSpriteLoader;
  }

  DropdownItem MakeStatusOption(string spriteName) {
    return new DropdownItem {
        Value = spriteName,
        Icon = _statusSpriteLoader.LoadSprite(spriteName),
        Text = spriteName.Split('/', '\\').Last(),
    };
  }

  #endregion

  #region Helper BaseComponent to show blocked status

  internal sealed class StatusController : AbstractStatusTracker, IPersistentEntity {

    #region API

    /// <summary>Full definition of the status.</summary>
    [ProtoContract]
    public record struct StatusDef() {
      [ProtoMember(1)] public string SpriteName { get; init; } = null;
      [ProtoMember(2)] public string StatusTextLocKey { get; init; } = "";
      [ProtoMember(3)] public string AlertTextLocKey { get; init; } = "";
      [ProtoMember(4)] public bool NeedFloatingIcon { get; init; } = false;
      [ProtoMember(5)] public bool IsPriority { get; init; } = false;
    }

    public void SetStatusState(StatusDef statusDef, bool newState) {
      if (_currentStatusDef != statusDef) {
        _currentStatusDef = statusDef;
        _currentStatusToggle?.Deactivate();
        _currentStatusToggle = GetOrCreateToggle(statusDef);
      }
      _currentStatusToggle.Toggle(newState);
    }

    public void ClearCurrentStatus() {
      _currentStatusToggle?.Deactivate();
      _currentStatusToggle = null;
      _currentStatusDef = default;
    }

    #endregion

    #region IPersistentEntity implementation

    static readonly ComponentKey StatusControllerKey = new(typeof(StatusController).FullName);
    static readonly PropertyKey<string> StatusStateKey = new("StatusState");

    /// <inheritdoc/>
    public void Save(IEntitySaver entitySaver) {
      if (_currentStatusToggle is not { IsActive: true }) {
        return;
      }
      var component = entitySaver.GetComponent(StatusControllerKey);
      component.Set(StatusStateKey, StringProtoSerializer.Serialize(_currentStatusDef));
    }

    /// <inheritdoc/>
    public void Load(IEntityLoader entityLoader) {
      if (!entityLoader.TryGetComponent(StatusControllerKey, out var component)) {
        return;
      }
      var statusDef = StringProtoSerializer.Deserialize<StatusDef>(component.Get(StatusStateKey));
      SetStatusState(statusDef, true);
    }
    #endregion

    #region AbstractStatusTracker implementation

    /// <inheritdoc/>
    protected override void OnLastReference() {
      ClearCurrentStatus();
    }

    #endregion

    #region Implementation

    // There is no reliable mechanism to unregister unused toggles, so we keep all of them.
    readonly Dictionary<StatusDef, StatusToggle> _statusToggles = new();
    StatusToggle _currentStatusToggle;
    StatusDef _currentStatusDef;

    StatusToggle GetOrCreateToggle(StatusDef statusDef) {
      if (_statusToggles.TryGetValue(statusDef, out var statusToggle)) {
        return statusToggle;
      }
      var statusText = statusDef.StatusTextLocKey.StartsWith("#")
          ? AutomationBehavior.Loc.T(statusDef.StatusTextLocKey[1..])
          : statusDef.StatusTextLocKey;
      var alertText = statusDef.AlertTextLocKey.StartsWith("#")
          ? AutomationBehavior.Loc.T(statusDef.AlertTextLocKey[1..])
          : statusDef.AlertTextLocKey;
      if (alertText != "") {
        // Alerts are grouped in the UI fragment by the alert text only.
        // The patch will remove the prefix before presenting the string in UI. 
        alertText = $"{statusDef.SpriteName}###{alertText}";
      }
      if (statusDef.IsPriority) {
        statusToggle = alertText != ""
            ? StatusToggle.CreatePriorityStatusWithAlertAndFloatingIcon(statusDef.SpriteName, statusText, alertText)
            : StatusToggle.CreatePriorityStatusWithFloatingIcon(statusDef.SpriteName, statusText);
      } else if (statusDef.NeedFloatingIcon || alertText != "") {
        statusToggle = alertText != ""
            ? StatusToggle.CreateNormalStatusWithAlertAndFloatingIcon(statusDef.SpriteName, statusText, alertText)
            : StatusToggle.CreateNormalStatusWithFloatingIcon(statusDef.SpriteName, statusText);
      } else {
        statusToggle = StatusToggle.CreateNormalStatus(statusDef.SpriteName, statusText);
      }
      try {
        AutomationBehavior.GetComponent<StatusSubject>().RegisterStatus(statusToggle);
      } catch (InvalidOperationException) {
        throw new ScriptError.BadValue($"Unknown status icon name: {statusDef.SpriteName}");
      }
      _statusToggles.Add(statusDef, statusToggle);
      return statusToggle;
    }

    #endregion
  }

  #endregion
}

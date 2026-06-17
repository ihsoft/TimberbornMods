using System.Reflection;
using Bindito.Core;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.TimberDev.Utils;
using Timberborn.Bots;
using Timberborn.BlockSystem;
using Timberborn.DwellingSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.Localization;
using Timberborn.ResourceCountingSystem;

namespace Automation.Tests;

static class DistrictScriptableComponentTests {
  public static void ExposesSignalsForDistrictBuilding() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateDistrictCenter());

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("District.Beavers", signalNames[0]);
    Assert.Equal("District.Bots", signalNames[1]);
    Assert.Equal("District.NumberOfBeds", signalNames[2]);
  }

  public static void ExposesResourceSignalsSortedByGoodDisplayName() {
    var component = CreateComponent();
    var districtCenter = CreateDistrictCenter();
    var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
    resourceCounter._stockCounter.SetInputOutputStock("Log", 2);
    resourceCounter._capacityCounter.SetOutputCapacity("Plank", 10);
    var behavior = CreateBehavior(districtCenter);

    var signalNames = component.GetSignalNamesForBuilding(behavior);

    Assert.Equal("District.ResourceStock.Plank", signalNames[3]);
    Assert.Equal("District.ResourceCapacity.Plank", signalNames[4]);
    Assert.Equal("District.ResourceStock.Log", signalNames[5]);
    Assert.Equal("District.ResourceCapacity.Log", signalNames[6]);
  }

  public static void HidesSignalsForMissingDistrictBuilding() {
    var component = CreateComponent();

    Assert.Equal(0, component.GetSignalNamesForBuilding(new AutomationBehavior()).Length);
  }

  public static void ReadsPopulationBedsAndResourceSignals() {
    var component = CreateComponent();
    var districtCenter = CreateDistrictCenter();
    var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
    resourceCounter._stockCounter.SetInputOutputStock("Log", 4);
    resourceCounter._stockCounter.SetOutputStock("Log", 3);
    resourceCounter._capacityCounter.SetInputOutputCapacity("Log", 12);
    districtCenter.DistrictPopulation.AssignBeaver(new Citizen());
    districtCenter.DistrictPopulation.AssignBot(new Citizen());
    districtCenter.GetComponent<DistrictDwellingStatisticsProvider>().Statistics = new DwellingStatistics {
        FreeBeds = 2,
        OccupiedBeds = 5,
    };
    var behavior = CreateBehavior(districtCenter);

    Assert.Equal(1, component.GetSignalSource("District.Beavers", behavior)().AsInt);
    Assert.Equal(1, component.GetSignalSource("District.Bots", behavior)().AsInt);
    Assert.Equal(7, component.GetSignalSource("District.NumberOfBeds", behavior)().AsInt);
    Assert.Equal(7, component.GetSignalSource("District.ResourceStock.Log", behavior)().AsInt);
    Assert.Equal(12, component.GetSignalSource("District.ResourceCapacity.Log", behavior)().AsInt);
  }

  public static void BuildsSignalDefinitions() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateDistrictCenter());

    var beaversDef = component.GetSignalDefinition("District.Beavers", behavior);
    var stockDef = component.GetSignalDefinition("District.ResourceStock.Log", behavior);
    var capacityDef = component.GetSignalDefinition("District.ResourceCapacity.Log", behavior);

    Assert.Equal("District.Beavers", beaversDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.District.Signal.Beavers", beaversDef.DisplayName);
    Assert.Equal((0, float.NaN), beaversDef.Result.DisplayNumericFormatRange);
    Assert.Equal("District.ResourceStock.Log", stockDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.District.Signal.ResourceStock:Logs", stockDef.DisplayName);
    Assert.Equal("District.ResourceCapacity.Log", capacityDef.ScriptName);
    Assert.Equal("IgorZ.Automation.Scriptable.District.Signal.ResourceCapacity:Logs", capacityDef.DisplayName);
  }

  public static void TickUpdatesTrackedResourcesAndNotifiesListeners() {
    var component = CreateComponent();
    var districtCenter = CreateDistrictCenter();
    var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
    resourceCounter._stockCounter.SetInputOutputStock("Log", 4);
    resourceCounter._capacityCounter.SetInputOutputCapacity("Log", 12);
    var behavior = CreateBehavior(districtCenter, withDynamicComponents: true);
    var stockListener = new TestSignalListener(behavior);
    var capacityListener = new TestSignalListener(behavior);

    component.RegisterSignalChangeCallback(Signal("District.ResourceStock.Log", behavior), stockListener);
    component.RegisterSignalChangeCallback(Signal("District.ResourceCapacity.Log", behavior), capacityListener);
    resourceCounter._stockCounter.SetInputOutputStock("Log", 6);
    resourceCounter._stockCounter.SetOutputStock("Log", 1);
    resourceCounter._capacityCounter.SetInputOutputCapacity("Log", 18);
    component.Tick();

    Assert.Equal(7, component.GetSignalSource("District.ResourceStock.Log", behavior)().AsInt);
    Assert.Equal(18, component.GetSignalSource("District.ResourceCapacity.Log", behavior)().AsInt);
    Assert.Equal(1, stockListener.Calls);
    Assert.Equal("District.ResourceStock.Log", stockListener.LastSignalName);
    Assert.Equal(1, capacityListener.Calls);
    Assert.Equal("District.ResourceCapacity.Log", capacityListener.LastSignalName);
  }

  public static void TickResetsTrackedResourcesWhenDistrictDisconnects() {
    var component = CreateComponent();
    var districtCenter = CreateDistrictCenter();
    var resourceCounter = districtCenter.GetComponent<DistrictResourceCounter>();
    resourceCounter._stockCounter.SetInputOutputStock("Log", 4);
    resourceCounter._capacityCounter.SetInputOutputCapacity("Log", 12);
    var behavior = CreateBehavior(districtCenter, withDynamicComponents: true);
    var districtBuilding = behavior.GetComponent<DistrictBuilding>();
    var stockListener = new TestSignalListener(behavior);
    var capacityListener = new TestSignalListener(behavior);

    component.RegisterSignalChangeCallback(Signal("District.ResourceStock.Log", behavior), stockListener);
    component.RegisterSignalChangeCallback(Signal("District.ResourceCapacity.Log", behavior), capacityListener);
    districtBuilding.SetDistrict(null);
    component.Tick();

    Assert.Equal(0, component.GetSignalSource("District.ResourceStock.Log", behavior)().AsInt);
    Assert.Equal(0, component.GetSignalSource("District.ResourceCapacity.Log", behavior)().AsInt);
    Assert.Equal(1, stockListener.Calls);
    Assert.Equal(1, capacityListener.Calls);
  }

  public static void PopulationEventsNotifyMatchingListeners() {
    var component = CreateComponent();
    var districtCenter = CreateDistrictCenter();
    var behavior = CreateBehavior(districtCenter, withDynamicComponents: true);
    var beaverListener = new TestSignalListener(behavior);
    var botListener = new TestSignalListener(behavior);
    var bot = new Citizen();
    bot.SetComponent(new BotSpec());

    component.RegisterSignalChangeCallback(Signal("District.Beavers", behavior), beaverListener);
    component.RegisterSignalChangeCallback(Signal("District.Bots", behavior), botListener);
    districtCenter.DistrictPopulation.AssignBeaver(new Citizen());
    districtCenter.DistrictPopulation.AssignBot(bot);

    Assert.Equal(1, beaverListener.Calls);
    Assert.Equal("District.Beavers", beaverListener.LastSignalName);
    Assert.Equal(1, botListener.Calls);
    Assert.Equal("District.Bots", botListener.LastSignalName);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();
    var behavior = CreateBehavior(CreateDistrictCenter());

    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalSource("District.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(() => component.GetSignalDefinition("District.Missing", behavior));
    Assert.Throws<ScriptError.ParsingError>(
        () => component.GetSignalDefinition("District.ResourceStock.Missing", behavior));
  }

  static DistrictScriptableComponent CreateComponent() {
    SetDependencyContainer(new TestContainer());
    var constructor = typeof(DistrictScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IGoodService)],
        null);
    var component = (DistrictScriptableComponent)constructor.Invoke([new TestGoodService()]);
    component.InjectDependencies(new TestLoc(), TestScripting.CreateService());
    component.Load();
    return component;
  }

  static AutomationBehavior CreateBehavior(DistrictCenter districtCenter, bool withDynamicComponents = false) {
    if (withDynamicComponents) {
      SetDependencyContainer(new TestContainer());
    }
    var behavior = new AutomationBehavior();
    behavior.SetComponent(new BlockObject());
    var districtBuilding = new DistrictBuilding();
    districtBuilding.SetDistrict(districtCenter);
    behavior.SetComponent(districtBuilding);
    if (withDynamicComponents) {
      behavior.InjectDependencies(new AutomationService());
      behavior.Awake();
      behavior.InitializeEntity();
    }
    return behavior;
  }

  static DistrictCenter CreateDistrictCenter() {
    var districtCenter = new DistrictCenter();
    districtCenter.SetComponent(new DistrictResourceCounter());
    districtCenter.SetComponent(new DistrictDwellingStatisticsProvider());
    return districtCenter;
  }

  static SignalOperator Signal(string signalName, AutomationBehavior behavior) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = behavior }, signalName);
  }

  static void SetDependencyContainer(IContainer container) {
    var constructor = typeof(StaticBindings).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [typeof(IContainer)],
        null);
    constructor.Invoke([container]);
  }

  sealed class TestGoodService : IGoodService {
    public GoodSpec GetGood(string id) {
      return GetGoodOrNull(id) ?? throw new System.InvalidOperationException("Unknown good: " + id);
    }

    public GoodSpec GetGoodOrNull(string id) {
      return id switch {
          "Log" => Good(id, "Logs"),
          "Plank" => Good(id, "Boards"),
          _ => null,
      };
    }

    static GoodSpec Good(string id, string pluralName) {
      return new GoodSpec {
          Id = id,
          PluralDisplayName = new LocalizedText(pluralName),
      };
    }
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return args.Length == 0 ? key : key + ":" + string.Join(",", args);
    }
  }

  sealed class TestSignalListener(AutomationBehavior behavior) : ISignalListener {
    public AutomationBehavior Behavior { get; } = behavior;
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
    }
  }
}

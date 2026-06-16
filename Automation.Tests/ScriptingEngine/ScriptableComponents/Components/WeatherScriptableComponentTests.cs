using System;
using System.Reflection;
using IgorZ.Automation.AutomationSystem;
using IgorZ.Automation.ScriptingEngine.Core;
using IgorZ.Automation.ScriptingEngine.Expressions;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Components;
using IgorZ.Automation.ScriptingEngine.ScriptableComponents.Extensions;
using Timberborn.HazardousWeatherSystem;
using Timberborn.Localization;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using Timberborn.WorldPersistence;
using UnityEngine;

namespace Automation.Tests;

static class WeatherScriptableComponentTests {
  public static void RegistersWeatherExtension() {
    var registry = new AutomationExtensionsRegistry();
    var component = CreateComponent(registry: registry);

    Assert.Same(component, registry.GetExtension<IWeatherExtension>());
  }

  public static void ExposesSeasonSignalAndDefinitions() {
    var component = CreateComponent();
    component.AddWeatherId("CustomWeather", "Weather.Custom");

    var signalNames = component.GetSignalNamesForBuilding(new AutomationBehavior());
    var signalDef = component.GetSignalDefinition("Weather.Season", new AutomationBehavior());

    Assert.Equal("Weather.Season", signalNames[0]);
    Assert.Equal("Weather.Season", signalDef.ScriptName);
    Assert.Equal("CustomWeather", signalDef.Result.Options[0].Value);
    Assert.Equal("TemperateWeather", signalDef.Result.Options[1].Value);
  }

  public static void InitializesAndSavesCurrentSeason() {
    var component = CreateComponent();
    component.Load();
    component.PostLoad();

    Assert.Equal("TemperateWeather", component.GetSignalSource("Weather.Season", new AutomationBehavior())().AsString);

    var saver = new TestSingletonSaver();
    component.Save(saver);

    Assert.Equal("TemperateWeather", saver.GetSavedValue("WeatherStateRestorer", "CurrentSeason"));
  }

  public static void UsesTemperateWeatherProviderAndNotifiesListeners() {
    var component = CreateComponent();
    component.Load();
    component.PostLoad();
    component.AddTemperateWeatherIdProvider(() => "CustomTemperate");
    var listener = new TestSignalListener();
    component.RegisterSignalChangeCallback(Signal("Weather.Season"), listener);

    component.TriggerSeasonCheck();
    MonoBehaviour.RunQueuedCoroutines();

    Assert.Equal("CustomTemperate", component.GetSignalSource("Weather.Season", new AutomationBehavior())().AsString);
    Assert.Equal(1, listener.Calls);
    Assert.Equal("Weather.Season", listener.LastSignalName);
  }

  public static void UsesHazardousWeatherWhenActive() {
    var weatherService = new WeatherService { IsHazardousWeather = true };
    var hazardousWeatherService = new HazardousWeatherService {
        CurrentCycleHazardousWeather = new HazardousWeather { Id = "BadtideWeather" },
    };
    var component = CreateComponent(weatherService: weatherService, hazardousWeatherService: hazardousWeatherService);
    component.Load();
    component.PostLoad();

    Assert.Equal("BadtideWeather", component.GetSignalSource("Weather.Season", new AutomationBehavior())().AsString);
  }

  public static void ReportsUnknownSignal() {
    var component = CreateComponent();

    Assert.Throws<ScriptError.ParsingError>(
        () => component.GetSignalDefinition("Weather.Missing", new AutomationBehavior()));
  }

  static WeatherScriptableComponent CreateComponent(
      AutomationExtensionsRegistry registry = null, WeatherService weatherService = null,
      HazardousWeatherService hazardousWeatherService = null) {
    var service = TestScripting.CreateService();
    registry ??= new AutomationExtensionsRegistry();
    weatherService ??= new WeatherService();
    hazardousWeatherService ??= new HazardousWeatherService {
        CurrentCycleHazardousWeather = new HazardousWeather { Id = "DroughtWeather" },
    };
    var component = CreateWithPrivateConstructor(
        new EventBus(),
        weatherService,
        new ReferenceManager(service),
        hazardousWeatherService,
        new EmptySingletonLoader(),
        registry);
    component.InjectDependencies(new TestLoc(), service);
    return component;
  }

  static WeatherScriptableComponent CreateWithPrivateConstructor(params object[] args) {
    var constructor = typeof(WeatherScriptableComponent).GetConstructor(
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [
            typeof(EventBus),
            typeof(WeatherService),
            typeof(ReferenceManager),
            typeof(HazardousWeatherService),
            typeof(ISingletonLoader),
            typeof(AutomationExtensionsRegistry),
        ],
        null);
    return (WeatherScriptableComponent)constructor.Invoke(args);
  }

  static SignalOperator Signal(string signalName) {
    return SignalOperator.Create(new ExpressionContext { ScriptHost = new AutomationBehavior() }, signalName);
  }

  sealed class EmptySingletonLoader : ISingletonLoader {
    public bool TryGetSingleton(
        Timberborn.Persistence.SingletonKey singletonKey, out Timberborn.Persistence.IObjectLoader objectLoader) {
      objectLoader = null;
      return false;
    }
  }

  sealed class TestSingletonSaver : ISingletonSaver {
    readonly System.Collections.Generic.Dictionary<string, Timberborn.Persistence.IObjectSaver> _singletons = new();

    public Timberborn.Persistence.IObjectSaver GetSingleton(Timberborn.Persistence.SingletonKey singletonKey) {
      var saver = new Timberborn.Persistence.IObjectSaver();
      _singletons[singletonKey.Name] = saver;
      return saver;
    }

    public string GetSavedValue(string singletonName, string propertyName) {
      return (string)_singletons[singletonName].Values[propertyName];
    }
  }

  sealed class TestSignalListener : ISignalListener {
    public AutomationBehavior Behavior { get; } = new();
    public int Calls { get; private set; }
    public string LastSignalName { get; private set; }

    public void OnValueChanged(string signalName) {
      Calls++;
      LastSignalName = signalName;
    }
  }

  sealed class TestLoc : ILoc {
    public string T(string key, params object[] args) {
      return key;
    }
  }
}
